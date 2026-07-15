//! Single-consumer job queue that serializes pipeline execution and streams
//! per-stage events to the connected UI client (spec/api.md §2.2-2.3).
//!
//! This replaces the old `execution_lock` mutex: since HTTP handlers now
//! return an ack before the pipeline runs, the lock could no longer be
//! acquired synchronously in the handler without blocking the ack on
//! pipeline completion. Instead, jobs are sent (non-blocking, no `.await`)
//! to this queue in the order handlers receive requests, and this single
//! worker loop processes them strictly in receive order — the loop itself
//! is the serialization point, matching CLAUDE.md §3.3 / architecture.md §6.

use std::path::PathBuf;
use std::sync::Arc;

use tokio::sync::mpsc;

use crate::error::Error;
use crate::model::ModelAdapter;
use crate::{evaluator, ir, normalizer, parser};

use super::dto::{self, Event};
use super::SharedState;

pub enum JobKind {
    Run,
    Explain,
    Trace,
}

pub struct PipelineJob {
    pub kind: JobKind,
    pub source: String,
    pub correlation_id: String,
}

fn current_dir_or_dot() -> PathBuf {
    std::env::current_dir().unwrap_or_else(|_| PathBuf::from("."))
}

pub async fn run_worker(mut job_rx: mpsc::UnboundedReceiver<PipelineJob>, state: SharedState) {
    while let Some(job) = job_rx.recv().await {
        run_job(job, &state).await;
    }
}

async fn run_job(job: PipelineJob, state: &SharedState) {
    let PipelineJob {
        kind,
        source,
        correlation_id,
    } = job;
    state.client_tx.send(Event::started(correlation_id.clone()));

    match kind {
        JobKind::Run => run_run(source, correlation_id, Arc::clone(&state.adapter), state).await,
        JobKind::Explain => run_explain(source, correlation_id, state).await,
        JobKind::Trace => {
            run_trace(source, correlation_id, Arc::clone(&state.adapter), state).await
        }
    }
}

// ---------------------------------------------------------------------------
// /run: pipeline_started -> final_result_ready
// ---------------------------------------------------------------------------

async fn run_run(
    source: String,
    correlation_id: String,
    adapter: Arc<dyn ModelAdapter + Send + Sync>,
    state: &SharedState,
) {
    let result = tokio::task::spawn_blocking(move || -> Result<evaluator::EvalOutcome, Error> {
        let base_dir = current_dir_or_dot();
        let raw_ast = parser::parse(&source)?;
        let normalized_ast = normalizer::normalize(&raw_ast)?;
        let program = ir::compiler::compile(&normalized_ast)?;
        evaluator::evaluate(
            &program,
            adapter.as_ref(),
            &base_dir,
            false,
            None,
            None,
            None,
        )
    })
    .await
    .expect("pipeline task panicked");

    match result {
        Ok(outcome) => state.client_tx.send(Event::ok(
            dto::EVENT_FINAL_RESULT_READY,
            correlation_id,
            dto::RunData {
                final_result: dto::final_result(&outcome.final_result),
            },
        )),
        Err(e) => state
            .client_tx
            .send(Event::failed(correlation_id, dto::map_error(&e))),
    }
}

// ---------------------------------------------------------------------------
// /explain: pipeline_started -> raw_ast_generated -> normalized_ast_generated
// (never invokes the evaluator, so there is no final_result_ready)
//
// Each stage runs in its own `spawn_blocking`, and its event is sent
// immediately after that stage returns and before the next stage begins,
// per spec/api.md §2.1 "Event Emission Timing".
// ---------------------------------------------------------------------------

async fn run_explain(source: String, correlation_id: String, state: &SharedState) {
    let parse_result = tokio::task::spawn_blocking(move || {
        parser::parse(&source).map(|raw_ast| (raw_ast, source))
    })
    .await
    .expect("pipeline task panicked");
    let (raw_ast, source) = match parse_result {
        Ok(pair) => pair,
        Err(e) => {
            return state
                .client_tx
                .send(Event::failed(correlation_id, dto::map_error(&e)))
        }
    };
    state.client_tx.send(Event::ok(
        dto::EVENT_RAW_AST_GENERATED,
        correlation_id.clone(),
        dto::RawAstEventData {
            raw_ast: dto::raw_ast_response(&raw_ast, &source),
        },
    ));

    let normalize_result = tokio::task::spawn_blocking(move || {
        let normalized_ast = normalizer::normalize(&raw_ast)?;
        Ok::<_, Error>((normalized_ast, raw_ast))
    })
    .await
    .expect("pipeline task panicked");
    let (normalized_ast, raw_ast) = match normalize_result {
        Ok(pair) => pair,
        Err(e) => {
            return state
                .client_tx
                .send(Event::failed(correlation_id, dto::map_error(&e)))
        }
    };
    state.client_tx.send(Event::ok(
        dto::EVENT_NORMALIZED_AST_GENERATED,
        correlation_id.clone(),
        dto::NormalizedAstEventData {
            normalized_ast: dto::normalized_ast_response(&normalized_ast, &raw_ast),
        },
    ));

    // Validate the program fully compiles, matching `llx explain`'s existing
    // behavior — the IR itself isn't part of /explain's output, so no event
    // is emitted for this stage on success, only on failure.
    let compile_result =
        tokio::task::spawn_blocking(move || ir::compiler::compile(&normalized_ast))
            .await
            .expect("pipeline task panicked");
    if let Err(e) = compile_result {
        state
            .client_tx
            .send(Event::failed(correlation_id, dto::map_error(&e)));
    }
}

// ---------------------------------------------------------------------------
// /trace: pipeline_started -> raw_ast_generated -> normalized_ast_generated
// -> ir_generated -> (prompt_generated -> model_output_generated) x N
// -> final_result_ready
//
// Same per-stage `spawn_blocking` pattern as `run_explain`. `ir_generated`
// (and everything before it) is emitted, and therefore observable on the
// WebSocket, strictly before `evaluator::evaluate` — and therefore any model
// adapter call — begins (spec/api.md §2.1).
//
// `prompt_generated`/`model_output_generated` are streamed from *inside*
// `evaluator::evaluate`'s execution via a `TraceObserver` passed as its
// `observer` argument (spec/evaluator-semantics.md §4.1) — one pair per
// model-calling IR operation, emitted as it happens rather than batched from
// the returned `EvalOutcome` after the whole program finishes.
// ---------------------------------------------------------------------------

/// Forwards evaluator observer callbacks to the WebSocket as they occur,
/// while `evaluator::evaluate` is still executing (spec/api.md §2.1).
struct TraceObserver {
    state: SharedState,
    correlation_id: String,
}

impl evaluator::EvaluatorObserver for TraceObserver {
    fn on_prompt_generated(&self, operation_index: usize, prompt_text: &str) {
        self.state.client_tx.send(Event::ok(
            dto::EVENT_PROMPT_GENERATED,
            self.correlation_id.clone(),
            dto::PromptEventData {
                prompt: dto::prompt_block(operation_index, prompt_text),
            },
        ));
    }

    fn on_model_output_generated(&self, operation_index: usize, raw_text: &str, latency_ms: u128) {
        self.state.client_tx.send(Event::ok(
            dto::EVENT_MODEL_OUTPUT_GENERATED,
            self.correlation_id.clone(),
            dto::ModelOutputEventData {
                model_output: dto::model_output_block(operation_index, raw_text, latency_ms),
            },
        ));
    }
}

async fn run_trace(
    source: String,
    correlation_id: String,
    adapter: Arc<dyn ModelAdapter + Send + Sync>,
    state: &SharedState,
) {
    let base_dir = current_dir_or_dot();

    let parse_result = tokio::task::spawn_blocking(move || {
        parser::parse(&source).map(|raw_ast| (raw_ast, source))
    })
    .await
    .expect("pipeline task panicked");
    let (raw_ast, source) = match parse_result {
        Ok(pair) => pair,
        Err(e) => {
            return state
                .client_tx
                .send(Event::failed(correlation_id, dto::map_error(&e)))
        }
    };
    state.client_tx.send(Event::ok(
        dto::EVENT_RAW_AST_GENERATED,
        correlation_id.clone(),
        dto::RawAstEventData {
            raw_ast: dto::raw_ast_response(&raw_ast, &source),
        },
    ));

    let normalize_result = tokio::task::spawn_blocking(move || {
        let normalized_ast = normalizer::normalize(&raw_ast)?;
        Ok::<_, Error>((normalized_ast, raw_ast))
    })
    .await
    .expect("pipeline task panicked");
    let (normalized_ast, raw_ast) = match normalize_result {
        Ok(pair) => pair,
        Err(e) => {
            return state
                .client_tx
                .send(Event::failed(correlation_id, dto::map_error(&e)))
        }
    };
    state.client_tx.send(Event::ok(
        dto::EVENT_NORMALIZED_AST_GENERATED,
        correlation_id.clone(),
        dto::NormalizedAstEventData {
            normalized_ast: dto::normalized_ast_response(&normalized_ast, &raw_ast),
        },
    ));

    let compile_result =
        tokio::task::spawn_blocking(move || ir::compiler::compile(&normalized_ast))
            .await
            .expect("pipeline task panicked");
    let program = match compile_result {
        Ok(program) => program,
        Err(e) => {
            return state
                .client_tx
                .send(Event::failed(correlation_id, dto::map_error(&e)))
        }
    };
    state.client_tx.send(Event::ok(
        dto::EVENT_IR_GENERATED,
        correlation_id.clone(),
        dto::IrEventData {
            ir: dto::ir_response(&program),
        },
    ));

    let state_for_eval = Arc::clone(state);
    let correlation_id_for_eval = correlation_id.clone();
    let eval_result = tokio::task::spawn_blocking(move || {
        let observer = TraceObserver {
            state: state_for_eval,
            correlation_id: correlation_id_for_eval,
        };
        // trace=false: this is a server process, not a CLI invocation — no
        // stdout printing per request.
        evaluator::evaluate(
            &program,
            adapter.as_ref(),
            &base_dir,
            false,
            None,
            None,
            Some(&observer),
        )
    })
    .await
    .expect("pipeline task panicked");
    let outcome = match eval_result {
        Ok(outcome) => outcome,
        Err(e) => {
            return state
                .client_tx
                .send(Event::failed(correlation_id, dto::map_error(&e)))
        }
    };

    state.client_tx.send(Event::ok(
        dto::EVENT_FINAL_RESULT_READY,
        correlation_id,
        dto::RunData {
            final_result: dto::final_result(&outcome.final_result),
        },
    ));
}
