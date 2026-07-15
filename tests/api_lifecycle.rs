//! Integration tests for server lifecycle (spec/bdd-api.md §1) and
//! determinism / sequential execution (spec/bdd-api.md §6).

mod common;

use std::sync::{Arc, Mutex};
use std::time::{Duration, Instant};

use serde_json::json;

use limelight_x::error::Error;
use limelight_x::model::ModelAdapter;

// `cmd_serve`/`main`/`run` live in the `llx` binary target, not the
// `limelight_x` library, so these four scenarios spawn the real compiled
// binary as a subprocess (via `common::spawn_llx_serve` / `Command`
// directly) rather than calling library functions in-process — that's the
// only way to observe the actual port-binding, startup-line, and fail-fast
// behavior `llx serve` exhibits. None of these ever call `/run` or `/trace`
// against a real `ClaudeModelAdapter` (CLAUDE.md §6) — only `/explain`,
// which never invokes the model adapter.

#[test]
fn llx_serve_starts_and_binds_default_port() {
    let (_child, startup_line) = common::spawn_llx_serve(&[]);
    assert!(
        startup_line.contains("127.0.0.1:4747"),
        "unexpected startup line: {startup_line}"
    );

    let client = reqwest::blocking::Client::new();
    let response = client
        .post("http://127.0.0.1:4747/explain")
        .json(&json!({ "source": "Load the article from \"a.txt\"." }))
        .send()
        .expect("request should reach the server");
    assert_eq!(response.status().as_u16(), 200);
}

#[test]
fn llx_serve_respects_custom_port() {
    let (_child, startup_line) = common::spawn_llx_serve(&["--port", "9001"]);
    assert!(
        startup_line.contains("127.0.0.1:9001"),
        "unexpected startup line: {startup_line}"
    );
}

#[test]
fn llx_serve_port_in_use_fails_fast_at_process_level() {
    // Reserve a port with a plain std listener first.
    let occupied = std::net::TcpListener::bind("127.0.0.1:0").unwrap();
    let port = occupied.local_addr().unwrap().port();

    let output = std::process::Command::new(env!("CARGO_BIN_EXE_llx"))
        .args(["serve", "--port", &port.to_string()])
        .env("ANTHROPIC_API_KEY", "sk-test-dummy-key-not-a-real-key")
        .stdout(std::process::Stdio::piped())
        .stderr(std::process::Stdio::piped())
        .output()
        .expect("failed to spawn the llx binary");

    drop(occupied);

    assert!(
        !output.status.success(),
        "llx serve must exit non-zero when the port is already in use"
    );
    let stderr = String::from_utf8_lossy(&output.stderr);
    assert!(
        stderr.contains(&port.to_string()),
        "expected the error message to name the port {port}, got: {stderr}"
    );
}

#[test]
fn llx_serve_missing_api_key_fails_fast_at_process_level() {
    let mut cmd = std::process::Command::new(env!("CARGO_BIN_EXE_llx"));
    cmd.args(["serve", "--port", "0"])
        .env_remove("ANTHROPIC_API_KEY")
        .stdout(std::process::Stdio::piped())
        .stderr(std::process::Stdio::piped());
    let output = cmd.output().expect("failed to spawn the llx binary");

    assert!(
        !output.status.success(),
        "llx serve must exit non-zero when ANTHROPIC_API_KEY is unset"
    );
    let stderr = String::from_utf8_lossy(&output.stderr).to_lowercase();
    assert!(
        stderr.contains("missing environment variable: anthropic_api_key"),
        "unexpected stderr: {stderr}"
    );
}

/// Records the wall-clock interval of each `complete()` call, sleeping
/// briefly so overlapping calls (if any) would produce overlapping intervals.
struct RecordingAdapter {
    log: Arc<Mutex<Vec<(Instant, Instant)>>>,
}

impl ModelAdapter for RecordingAdapter {
    fn complete(&self, _prompt: &str) -> Result<String, Error> {
        let start = Instant::now();
        std::thread::sleep(Duration::from_millis(150));
        let end = Instant::now();
        self.log.lock().unwrap().push((start, end));
        Ok("output".to_string())
    }
}

// Needs a multi-thread runtime: the test body blocks on `JoinHandle::join()`
// for two OS threads, which would starve a single-threaded runtime of the
// worker thread the spawned server task (and its own spawn_blocking calls)
// needs to make progress on.
#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
async fn two_concurrent_requests_are_handled_sequentially() {
    let log = Arc::new(Mutex::new(Vec::new()));
    let adapter: Arc<dyn ModelAdapter + Send + Sync> = Arc::new(RecordingAdapter {
        log: Arc::clone(&log),
    });
    let base_url = common::spawn_test_server_with_adapter(adapter).await;
    let mut ws = common::TestWsClient::connect(&base_url).await;

    let path = common::make_temp_file("some text");
    let source = format!("Load the article from \"{path}\".\nSummarize it.");

    let url1 = base_url.clone();
    let url2 = base_url.clone();
    let source1 = source.clone();
    let source2 = source.clone();

    // Fire two requests concurrently from separate OS threads (reqwest::blocking).
    let t1 = std::thread::spawn(move || {
        common::post_json_blocking(&url1, "/run", json!({ "source": source1 }))
    });
    let t2 = std::thread::spawn(move || {
        common::post_json_blocking(&url2, "/run", json!({ "source": source2 }))
    });

    let (status1, ack1) = t1.join().unwrap();
    let (status2, ack2) = t2.join().unwrap();
    assert_eq!(status1, 200);
    assert_eq!(status2, 200);
    let id1 = ack1["correlation_id"].as_str().unwrap().to_string();
    let id2 = ack2["correlation_id"].as_str().unwrap().to_string();
    assert_ne!(id1, id2, "each request must get a distinct correlation_id");

    // The single-consumer worker (worker.rs) processes one job fully before
    // starting the next, so the four events must arrive grouped by
    // correlation_id: one job's `pipeline_started`+`final_result_ready`
    // pair, then the other's — never interleaved.
    let e1 = ws.recv_json().await;
    let e2 = ws.recv_json().await;
    let e3 = ws.recv_json().await;
    let e4 = ws.recv_json().await;

    assert_eq!(e1["event_type"], "pipeline_started");
    assert_eq!(e2["event_type"], "final_result_ready");
    assert_eq!(e1["correlation_id"], e2["correlation_id"]);

    assert_eq!(e3["event_type"], "pipeline_started");
    assert_eq!(e4["event_type"], "final_result_ready");
    assert_eq!(e3["correlation_id"], e4["correlation_id"]);

    assert_ne!(
        e1["correlation_id"], e3["correlation_id"],
        "the two jobs' events must not interleave"
    );

    let recorded = log.lock().unwrap();
    assert_eq!(recorded.len(), 2, "expected exactly two model calls");
    let (start_a, end_a) = recorded[0];
    let (start_b, end_b) = recorded[1];
    // No overlap in either order: one call's window must fully precede the other's.
    let sequential = end_a <= start_b || end_b <= start_a;
    assert!(sequential, "model calls overlapped: {recorded:?}");
}
