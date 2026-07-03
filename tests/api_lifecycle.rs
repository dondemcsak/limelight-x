//! Integration tests for server lifecycle (spec/bdd-api.md §1) and
//! determinism / sequential execution (spec/bdd-api.md §6).

mod common;

use std::sync::{Arc, Mutex};
use std::time::{Duration, Instant};

use serde_json::json;

use limelight_x::error::Error;
use limelight_x::model::ModelAdapter;

#[tokio::test]
async fn server_starts_and_responds() {
    let base_url = common::spawn_test_server("ok").await;
    let (status, body) = common::post_json(
        &base_url,
        "/explain",
        json!({ "source": "Load the article from \"a.txt\"." }),
    )
    .await;
    assert_eq!(status, 200);
    assert_eq!(body["version"], "v1");
}

#[tokio::test]
async fn starting_on_an_occupied_port_fails() {
    // Reserve a port with a plain std listener first.
    let occupied = std::net::TcpListener::bind("127.0.0.1:0").unwrap();
    let port = occupied.local_addr().unwrap().port();

    let adapter: Arc<dyn ModelAdapter + Send + Sync> =
        Arc::new(limelight_x::model::mock::MockModelAdapter::new("unused"));
    let result = limelight_x::api::serve(port, adapter).await;

    assert!(
        result.is_err(),
        "expected a bind failure on an already-occupied port"
    );
    drop(occupied);
}

#[test]
fn missing_api_key_fails_fast() {
    // Save/restore so this doesn't leak into other tests in this binary.
    let previous = std::env::var("ANTHROPIC_API_KEY").ok();
    std::env::remove_var("ANTHROPIC_API_KEY");

    let result = limelight_x::model::claude::ClaudeModelAdapter::new();
    assert!(matches!(result, Err(Error::MissingApiKey)));

    if let Some(value) = previous {
        std::env::set_var("ANTHROPIC_API_KEY", value);
    }
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

    let (status1, _) = t1.join().unwrap();
    let (status2, _) = t2.join().unwrap();
    assert_eq!(status1, 200);
    assert_eq!(status2, 200);

    let recorded = log.lock().unwrap();
    assert_eq!(recorded.len(), 2, "expected exactly two model calls");
    let (start_a, end_a) = recorded[0];
    let (start_b, end_b) = recorded[1];
    // No overlap in either order: one call's window must fully precede the other's.
    let sequential = end_a <= start_b || end_b <= start_a;
    assert!(sequential, "model calls overlapped: {recorded:?}");
}
