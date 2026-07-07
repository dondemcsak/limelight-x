//! Integration tests for `POST /trace` — spec/bdd-api.md §4.

mod common;

use std::sync::Arc;
use std::time::Duration;

#[tokio::test]
async fn trace_stage_events_are_emitted_incrementally_not_batched() {
    let delay = Duration::from_millis(100);
    let adapter = Arc::new(common::DelayedAdapter::new("summary text", delay));
    let base_url = common::spawn_test_server_with_adapter(adapter.clone()).await;
    let path = common::make_temp_file("article text");
    let source = format!("Load the article from \"{path}\".\nSummarize it.");

    let (correlation_id, mut ws) = common::start_and_connect(&base_url, "/trace", &source).await;

    let (t_started, started) = ws.recv_json_timed().await;
    assert_eq!(started["event_type"], "pipeline_started");
    assert_eq!(started["correlation_id"], correlation_id);

    let (t_raw, raw) = ws.recv_json_timed().await;
    assert_eq!(raw["event_type"], "raw_ast_generated");

    let (t_norm, normalized) = ws.recv_json_timed().await;
    assert_eq!(normalized["event_type"], "normalized_ast_generated");

    let (t_ir, ir) = ws.recv_json_timed().await;
    assert_eq!(ir["event_type"], "ir_generated");

    let (_t_prompts, prompts) = ws.recv_json_timed().await;
    assert_eq!(prompts["event_type"], "prompts_generated");

    let (t_model_outputs, model_outputs) = ws.recv_json_timed().await;
    assert_eq!(model_outputs["event_type"], "model_outputs_generated");

    let (t_final, final_event) = ws.recv_json_timed().await;
    assert_eq!(final_event["event_type"], "final_result_ready");

    assert!(
        t_raw < t_norm,
        "raw_ast_generated must precede normalized_ast_generated"
    );
    assert!(
        t_norm < t_ir,
        "normalized_ast_generated must precede ir_generated"
    );

    assert!(
        adapter.first_invocation().is_some(),
        "adapter must have been invoked exactly once"
    );

    assert!(t_model_outputs < t_final);

    let started_to_ir = t_ir.duration_since(t_started);
    assert!(
        started_to_ir < delay,
        "pipeline_started -> ir_generated took {started_to_ir:?}, not shorter \
         than the adapter's {delay:?} artificial delay — ir_generated is being \
         held back until after the model call (spec/api.md §2.1)"
    );
}

#[tokio::test]
async fn successful_trace_streams_full_pipeline_output_in_order() {
    let base_url = common::spawn_test_server("summary text").await;
    let path = common::make_temp_file("article text");
    let source = format!("Load the article from \"{path}\".\nSummarize it.");

    let (correlation_id, mut ws) = common::start_and_connect(&base_url, "/trace", &source).await;

    let started = ws.recv_json().await;
    assert_eq!(started["event_type"], "pipeline_started");
    assert_eq!(started["correlation_id"], correlation_id);

    let raw = ws.recv_json().await;
    assert_eq!(raw["event_type"], "raw_ast_generated");

    let normalized = ws.recv_json().await;
    assert_eq!(normalized["event_type"], "normalized_ast_generated");

    let ir = ws.recv_json().await;
    assert_eq!(ir["event_type"], "ir_generated");
    assert!(!ir["data"]["ir"]["operations"]
        .as_array()
        .unwrap()
        .is_empty());

    let prompts = ws.recv_json().await;
    assert_eq!(prompts["event_type"], "prompts_generated");
    assert!(!prompts["data"]["prompts"].as_array().unwrap().is_empty());

    let model_outputs = ws.recv_json().await;
    assert_eq!(model_outputs["event_type"], "model_outputs_generated");
    assert!(!model_outputs["data"]["model_outputs"]
        .as_array()
        .unwrap()
        .is_empty());

    let final_event = ws.recv_json().await;
    assert_eq!(final_event["event_type"], "final_result_ready");
    assert_eq!(final_event["correlation_id"], correlation_id);
    assert_eq!(final_event["data"]["final_result"]["text"], "summary text");
}

#[tokio::test]
async fn trace_prompts_match_evaluator_constructed_prompts_exactly() {
    let base_url = common::spawn_test_server("summary text").await;
    let path = common::make_temp_file("article text");
    // No custom prompt -> the built-in Summarize template, per evaluator-semantics.md.
    let source = format!("Load the article from \"{path}\".\nSummarize it.");

    let (_correlation_id, mut ws) = common::start_and_connect(&base_url, "/trace", &source).await;
    let _started = ws.recv_json().await;
    let _raw = ws.recv_json().await;
    let _normalized = ws.recv_json().await;
    let _ir = ws.recv_json().await;
    let prompts_event = ws.recv_json().await;

    let prompts = prompts_event["data"]["prompts"].as_array().unwrap();
    // Load isn't a model call, so only the Summarize op produces a prompt block.
    assert_eq!(prompts.len(), 1);
    let prompt_text = prompts[0]["prompt_text"].as_str().unwrap();
    assert_eq!(
        prompt_text,
        "Summarize the following text clearly and concisely:\n\narticle text"
    );
    assert_eq!(prompts[0]["operation_index"], 1);
}
