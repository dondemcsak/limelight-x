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
        evaluator::evaluate(&program, adapter.as_ref(), &base_dir, false, None, None)
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
// ---------------------------------------------------------------------------

async fn run_explain(source: String, correlation_id: String, state: &SharedState) {
    let result = tokio::task::spawn_blocking(move || {
        let raw_ast = parser::parse(&source)?;
        let normalized_ast = normalizer::normalize(&raw_ast)?;
        // Validate the program fully compiles, matching `llx explain`'s
        // existing behavior — the IR itself isn't part of /explain's output.
        let _program = ir::compiler::compile(&normalized_ast)?;
        Ok::<_, Error>((raw_ast, normalized_ast, source))
    })
    .await
    .expect("pipeline task panicked");

    match result {
        Ok((raw_ast, normalized_ast, source)) => {
            state.client_tx.send(Event::ok(
                dto::EVENT_RAW_AST_GENERATED,
                correlation_id.clone(),
                dto::RawAstEventData {
                    raw_ast: dto::raw_ast_response(&raw_ast, &source),
                },
            ));
            state.client_tx.send(Event::ok(
                dto::EVENT_NORMALIZED_AST_GENERATED,
                correlation_id,
                dto::NormalizedAstEventData {
                    normalized_ast: dto::normalized_ast_response(&normalized_ast, &raw_ast),
                },
            ));
        }
        Err(e) => state
            .client_tx
            .send(Event::failed(correlation_id, dto::map_error(&e))),
    }
}

// ---------------------------------------------------------------------------
// /trace: pipeline_started -> raw_ast_generated -> normalized_ast_generated
// -> ir_generated -> prompts_generated -> model_outputs_generated
// -> final_result_ready
// ---------------------------------------------------------------------------

async fn run_trace(
    source: String,
    correlation_id: String,
    adapter: Arc<dyn ModelAdapter + Send + Sync>,
    state: &SharedState,
) {
    let result = tokio::task::spawn_blocking(move || {
        let base_dir = current_dir_or_dot();
        let raw_ast = parser::parse(&source)?;
        let normalized_ast = normalizer::normalize(&raw_ast)?;
        let program = ir::compiler::compile(&normalized_ast)?;
        // trace=false: this is a server process, not a CLI invocation — no
        // stdout printing per request.
        let outcome =
            evaluator::evaluate(&program, adapter.as_ref(), &base_dir, false, None, None)?;
        Ok::<_, Error>((raw_ast, normalized_ast, program, outcome, source))
    })
    .await
    .expect("pipeline task panicked");

    match result {
        Ok((raw_ast, normalized_ast, program, outcome, source)) => {
            state.client_tx.send(Event::ok(
                dto::EVENT_RAW_AST_GENERATED,
                correlation_id.clone(),
                dto::RawAstEventData {
                    raw_ast: dto::raw_ast_response(&raw_ast, &source),
                },
            ));
            state.client_tx.send(Event::ok(
                dto::EVENT_NORMALIZED_AST_GENERATED,
                correlation_id.clone(),
                dto::NormalizedAstEventData {
                    normalized_ast: dto::normalized_ast_response(&normalized_ast, &raw_ast),
                },
            ));
            state.client_tx.send(Event::ok(
                dto::EVENT_IR_GENERATED,
                correlation_id.clone(),
                dto::IrEventData {
                    ir: dto::ir_response(&program),
                },
            ));
            state.client_tx.send(Event::ok(
                dto::EVENT_PROMPTS_GENERATED,
                correlation_id.clone(),
                dto::PromptsEventData {
                    prompts: dto::prompt_blocks(&outcome.prompts),
                },
            ));
            state.client_tx.send(Event::ok(
                dto::EVENT_MODEL_OUTPUTS_GENERATED,
                correlation_id.clone(),
                dto::ModelOutputsEventData {
                    model_outputs: dto::model_output_blocks(&outcome.model_outputs),
                },
            ));
            state.client_tx.send(Event::ok(
                dto::EVENT_FINAL_RESULT_READY,
                correlation_id,
                dto::RunData {
                    final_result: dto::final_result(&outcome.final_result),
                },
            ));
        }
        Err(e) => state
            .client_tx
            .send(Event::failed(correlation_id, dto::map_error(&e))),
    }
}
