//! HTTP handlers for `POST /run`, `/explain`, `/trace` (spec/api.md §2-9).
//!
//! Each handler: parses/validates the request body, acquires the shared
//! execution lock (spec/api.md §2 — no parallel pipeline execution), runs
//! the existing sync pipeline in a blocking task, and maps the result onto
//! the shared response envelope (spec/ux/ui-data-contracts.md §1).

use std::path::PathBuf;
use std::sync::Arc;

use axum::body::Bytes;
use axum::extract::State;
use axum::http::StatusCode;
use axum::Json;
use serde_json::Value;

use crate::error::Error;
use crate::{evaluator, ir, normalizer, parser};

use super::dto::{self, Envelope};
use super::SharedState;

/// Extracts the `source` field from a raw request body, distinguishing
/// syntactically malformed JSON from valid JSON missing the required field
/// (spec/api.md §10).
fn extract_source(bytes: &Bytes) -> Result<String, Envelope> {
    let text = String::from_utf8_lossy(bytes);
    let value: Value =
        serde_json::from_str(&text).map_err(|_| Envelope::err_one(dto::malformed_request_error()))?;
    match value.get("source").and_then(Value::as_str) {
        Some(s) => Ok(s.to_string()),
        None => Err(Envelope::err_one(dto::missing_field_error())),
    }
}

fn current_dir_or_dot() -> PathBuf {
    std::env::current_dir().unwrap_or_else(|_| PathBuf::from("."))
}

// ---------------------------------------------------------------------------
// POST /run
// ---------------------------------------------------------------------------

pub async fn run(State(state): State<SharedState>, bytes: Bytes) -> (StatusCode, Json<Envelope>) {
    let source = match extract_source(&bytes) {
        Ok(s) => s,
        Err(envelope) => return (StatusCode::BAD_REQUEST, Json(envelope)),
    };

    let _guard = state.execution_lock.lock().await;
    let adapter = Arc::clone(&state.adapter);

    let result = tokio::task::spawn_blocking(move || -> Result<evaluator::EvalOutcome, Error> {
        let base_dir = current_dir_or_dot();
        let raw_ast = parser::parse(&source)?;
        let normalized_ast = normalizer::normalize(&raw_ast)?;
        let program = ir::compiler::compile(&normalized_ast)?;
        evaluator::evaluate(&program, adapter.as_ref(), &base_dir, false, None, None)
    })
    .await
    .expect("pipeline task panicked");

    match result {
        Ok(outcome) => (
            StatusCode::OK,
            Json(Envelope::ok(dto::RunData {
                final_result: dto::final_result(&outcome.final_result),
            })),
        ),
        Err(e) => (StatusCode::OK, Json(Envelope::err_one(dto::map_error(&e)))),
    }
}

// ---------------------------------------------------------------------------
// POST /explain
// ---------------------------------------------------------------------------

pub async fn explain(State(state): State<SharedState>, bytes: Bytes) -> (StatusCode, Json<Envelope>) {
    let source = match extract_source(&bytes) {
        Ok(s) => s,
        Err(envelope) => return (StatusCode::BAD_REQUEST, Json(envelope)),
    };

    let _guard = state.execution_lock.lock().await;

    let result = tokio::task::spawn_blocking(move || {
        let raw_ast = parser::parse(&source)?;
        let normalized_ast = normalizer::normalize(&raw_ast)?;
        // Validate the program fully compiles, matching `llx explain`'s
        // existing behavior — the IR itself isn't part of /explain's response.
        let _program = ir::compiler::compile(&normalized_ast)?;
        Ok::<_, Error>((raw_ast, normalized_ast, source))
    })
    .await
    .expect("pipeline task panicked");

    match result {
        Ok((raw_ast, normalized_ast, source)) => (
            StatusCode::OK,
            Json(Envelope::ok(dto::ExplainData {
                raw_ast: dto::raw_ast_response(&raw_ast, &source),
                normalized_ast: dto::normalized_ast_response(&normalized_ast, &raw_ast),
            })),
        ),
        Err(e) => (StatusCode::OK, Json(Envelope::err_one(dto::map_error(&e)))),
    }
}

// ---------------------------------------------------------------------------
// POST /trace
// ---------------------------------------------------------------------------

pub async fn trace(State(state): State<SharedState>, bytes: Bytes) -> (StatusCode, Json<Envelope>) {
    let source = match extract_source(&bytes) {
        Ok(s) => s,
        Err(envelope) => return (StatusCode::BAD_REQUEST, Json(envelope)),
    };

    let _guard = state.execution_lock.lock().await;
    let adapter = Arc::clone(&state.adapter);

    let result = tokio::task::spawn_blocking(move || {
        let base_dir = current_dir_or_dot();
        let raw_ast = parser::parse(&source)?;
        let normalized_ast = normalizer::normalize(&raw_ast)?;
        let program = ir::compiler::compile(&normalized_ast)?;
        // trace=false: this is a server process, not a CLI invocation — no
        // stdout printing per request. Structured data still comes back via
        // EvalOutcome regardless (see evaluator::evaluate's doc comment).
        let outcome = evaluator::evaluate(&program, adapter.as_ref(), &base_dir, false, None, None)?;
        Ok::<_, Error>((raw_ast, normalized_ast, program, outcome, source))
    })
    .await
    .expect("pipeline task panicked");

    match result {
        Ok((raw_ast, normalized_ast, program, outcome, source)) => (
            StatusCode::OK,
            Json(Envelope::ok(dto::TraceData {
                raw_ast: dto::raw_ast_response(&raw_ast, &source),
                normalized_ast: dto::normalized_ast_response(&normalized_ast, &raw_ast),
                ir: dto::ir_response(&program),
                prompts: dto::prompt_blocks(&outcome.prompts),
                model_outputs: dto::model_output_blocks(&outcome.model_outputs),
            })),
        ),
        Err(e) => (StatusCode::OK, Json(Envelope::err_one(dto::map_error(&e)))),
    }
}
