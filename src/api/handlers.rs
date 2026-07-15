//! HTTP handlers for `POST /run`, `/explain`, `/trace` (spec/api.md §2).
//!
//! Each handler validates the request body synchronously and returns
//! immediately: an ack with a fresh `correlation_id` on success, or a plain
//! HTTP 400 error body on malformed/incomplete input (spec/api.md §10 —
//! these ack-phase failures never get a `correlation_id` or WS event, since
//! the request never enters the pipeline). The actual pipeline execution
//! happens asynchronously in the worker task (`worker.rs`), which streams
//! per-stage events to `/events`.

use std::sync::atomic::Ordering;

use axum::body::Bytes;
use axum::extract::State;
use axum::http::StatusCode;
use axum::Json;
use serde_json::Value;

use super::dto::{self, AckResponse, Envelope};
use super::worker::{JobKind, PipelineJob};
use super::SharedState;

/// Extracts the `source` field from a raw request body, distinguishing
/// syntactically malformed JSON from valid JSON missing the required field
/// (spec/api.md §10).
fn extract_source(bytes: &Bytes) -> Result<String, Envelope> {
    let text = String::from_utf8_lossy(bytes);
    let value: Value = serde_json::from_str(&text)
        .map_err(|_| Envelope::err_one(dto::malformed_request_error()))?;
    match value.get("source").and_then(Value::as_str) {
        Some(s) => Ok(s.to_string()),
        None => Err(Envelope::err_one(dto::missing_field_error())),
    }
}

fn next_correlation_id(state: &SharedState) -> String {
    let n = state.next_correlation_id.fetch_add(1, Ordering::Relaxed);
    format!("corr-{n}")
}

/// Allocates a `correlation_id`, enqueues the job for the worker task, and
/// returns the ack response immediately — no pipeline stage has run yet.
fn enqueue(state: &SharedState, kind: JobKind, source: String) -> (StatusCode, Json<Value>) {
    let correlation_id = next_correlation_id(state);
    let job = PipelineJob {
        kind,
        source,
        correlation_id: correlation_id.clone(),
    };
    // The worker task only exits when `job_tx` is dropped at server
    // shutdown, so this send cannot fail while the server is serving requests.
    state
        .job_tx
        .send(job)
        .expect("worker task must outlive the server");
    (
        StatusCode::OK,
        Json(
            serde_json::to_value(AckResponse {
                accepted: true,
                correlation_id,
            })
            .expect("AckResponse must be serializable"),
        ),
    )
}

fn ack_phase_error(envelope: Envelope) -> (StatusCode, Json<Value>) {
    (
        StatusCode::BAD_REQUEST,
        Json(serde_json::to_value(envelope).expect("Envelope must be serializable")),
    )
}

pub async fn run(State(state): State<SharedState>, bytes: Bytes) -> (StatusCode, Json<Value>) {
    match extract_source(&bytes) {
        Ok(source) => enqueue(&state, JobKind::Run, source),
        Err(envelope) => ack_phase_error(envelope),
    }
}

pub async fn explain(State(state): State<SharedState>, bytes: Bytes) -> (StatusCode, Json<Value>) {
    match extract_source(&bytes) {
        Ok(source) => enqueue(&state, JobKind::Explain, source),
        Err(envelope) => ack_phase_error(envelope),
    }
}

pub async fn trace(State(state): State<SharedState>, bytes: Bytes) -> (StatusCode, Json<Value>) {
    match extract_source(&bytes) {
        Ok(source) => enqueue(&state, JobKind::Trace, source),
        Err(envelope) => ack_phase_error(envelope),
    }
}
