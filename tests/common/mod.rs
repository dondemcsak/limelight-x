//! Shared test harness for `/src/api` integration tests (spec/bdd-api.md).
//!
//! Not a test binary itself — `tests/common/mod.rs` is Cargo's convention
//! for a helper module shared across `tests/*.rs` files. Each `tests/*.rs`
//! file compiles this module fresh into its own binary, and not every
//! helper is used by every file — hence `allow(dead_code)` here rather than
//! per-binary warnings.
#![allow(dead_code)]

mod subprocess;
mod ws_client;

use std::sync::{Arc, Mutex};
use std::time::{Duration, Instant};

use limelight_x::api;
use limelight_x::error::Error;
use limelight_x::model::mock::MockModelAdapter;
use limelight_x::model::ModelAdapter;

#[allow(unused_imports)]
pub use subprocess::spawn_llx_serve;
pub use ws_client::TestWsClient;

/// A `ModelAdapter` that sleeps for a configurable delay before returning a
/// fixed response, recording each invocation's `Instant` — proves a
/// pipeline-stage event was observed strictly before the model was ever
/// called (spec/bdd-api.md §4, "Trace pipeline stage events are emitted
/// incrementally"). Unlike `CapturingAdapter`'s `RefCell`, this is `Sync`, so
/// it can be wrapped in `Arc<dyn ModelAdapter + Send + Sync>` for `AppState`.
pub struct DelayedAdapter {
    response: String,
    delay: Duration,
    invocations: Mutex<Vec<Instant>>,
}

impl DelayedAdapter {
    pub fn new(response: impl Into<String>, delay: Duration) -> Self {
        Self {
            response: response.into(),
            delay,
            invocations: Mutex::new(Vec::new()),
        }
    }

    /// The `Instant` of this adapter's first `complete()` call, if any.
    pub fn first_invocation(&self) -> Option<Instant> {
        self.invocations.lock().unwrap().first().copied()
    }
}

impl ModelAdapter for DelayedAdapter {
    fn complete(&self, _prompt: &str) -> Result<String, Error> {
        self.invocations.lock().unwrap().push(Instant::now());
        std::thread::sleep(self.delay);
        Ok(self.response.clone())
    }
}

/// Starts `api::serve_on` on an OS-assigned ephemeral port in a background
/// task, using a mock model adapter that always returns `mock_response`.
/// Returns the base URL (e.g. `http://127.0.0.1:54321`) once bound.
pub async fn spawn_test_server(mock_response: &str) -> String {
    let adapter: Arc<dyn ModelAdapter + Send + Sync> =
        Arc::new(MockModelAdapter::new(mock_response));
    spawn_test_server_with_adapter(adapter).await
}

/// Same as [`spawn_test_server`], but with a caller-provided adapter (e.g. to
/// record call timing for the determinism scenarios).
pub async fn spawn_test_server_with_adapter(
    adapter: Arc<dyn ModelAdapter + Send + Sync>,
) -> String {
    let listener = tokio::net::TcpListener::bind("127.0.0.1:0")
        .await
        .expect("failed to bind ephemeral port");
    let port = listener.local_addr().unwrap().port();

    tokio::spawn(async move {
        api::serve_on(listener, adapter).await.unwrap();
    });

    format!("http://127.0.0.1:{port}")
}

// `reqwest::blocking` builds its own mini Tokio runtime internally, so it
// panics if called directly from inside an async/`#[tokio::test]` context.
// The `_sync` functions are safe to call from a plain OS thread (e.g. inside
// `std::thread::spawn`, as the determinism test does); the async wrappers
// below are for call sites inside `#[tokio::test]` bodies, and run the
// blocking call on Tokio's blocking thread pool via `spawn_blocking`.

pub fn post_json_blocking(
    base_url: &str,
    path: &str,
    body: serde_json::Value,
) -> (u16, serde_json::Value) {
    let client = reqwest::blocking::Client::new();
    let response = client
        .post(format!("{base_url}{path}"))
        .json(&body)
        .send()
        .expect("request should reach the test server");
    let status = response.status().as_u16();
    let json: serde_json::Value = response.json().expect("response should be valid JSON");
    (status, json)
}

pub fn post_raw_blocking(base_url: &str, path: &str, body: &str) -> (u16, serde_json::Value) {
    let client = reqwest::blocking::Client::new();
    let response = client
        .post(format!("{base_url}{path}"))
        .header("content-type", "application/json")
        .body(body.to_string())
        .send()
        .expect("request should reach the test server");
    let status = response.status().as_u16();
    let json: serde_json::Value = response.json().expect("response should be valid JSON");
    (status, json)
}

/// POSTs a JSON body and returns `(status, parsed body)`. Safe to call from
/// within a `#[tokio::test]` async body.
pub async fn post_json(
    base_url: &str,
    path: &str,
    body: serde_json::Value,
) -> (u16, serde_json::Value) {
    let base_url = base_url.to_string();
    let path = path.to_string();
    tokio::task::spawn_blocking(move || post_json_blocking(&base_url, &path, body))
        .await
        .expect("blocking task panicked")
}

/// POSTs a raw (possibly syntactically invalid) body, for malformed-request
/// scenarios. Safe to call from within a `#[tokio::test]` async body.
pub async fn post_raw(base_url: &str, path: &str, body: &str) -> (u16, serde_json::Value) {
    let base_url = base_url.to_string();
    let path = path.to_string();
    let body = body.to_string();
    tokio::task::spawn_blocking(move || post_raw_blocking(&base_url, &path, &body))
        .await
        .expect("blocking task panicked")
}

/// Connects a WebSocket client to `/events` *before* POSTing, so no event
/// can be dropped for lack of a connected client, then POSTs `{path}` with
/// `{ "source": source }` and asserts the ack shape. Returns the ack's
/// `correlation_id` alongside the connected client — callers read exactly as
/// many events as their scenario expects via `ws.recv_json()`.
pub async fn start_and_connect(base_url: &str, path: &str, source: &str) -> (String, TestWsClient) {
    let ws = TestWsClient::connect(base_url).await;
    let (status, ack) = post_json(base_url, path, serde_json::json!({ "source": source })).await;
    assert_eq!(status, 200, "expected an ack response, got: {ack}");
    assert_eq!(ack["accepted"], true);
    let correlation_id = ack["correlation_id"]
        .as_str()
        .expect("ack must include a correlation_id")
        .to_string();
    (correlation_id, ws)
}

/// Writes `content` to a fresh temp file and returns its absolute path as a
/// string — used so CNL `Load` statements don't depend on the test process's
/// current directory (the server resolves relative paths against its own cwd).
pub fn make_temp_file(content: &str) -> String {
    static COUNTER: std::sync::atomic::AtomicU64 = std::sync::atomic::AtomicU64::new(0);
    let id = COUNTER.fetch_add(1, std::sync::atomic::Ordering::SeqCst);
    let path = std::env::temp_dir().join(format!("llx_api_test_{id}.txt"));
    std::fs::write(&path, content).unwrap();
    path.to_str().unwrap().replace('\\', "/")
}
