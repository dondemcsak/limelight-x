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

    let (t_prompt, prompt) = ws.recv_json_timed().await;
    assert_eq!(prompt["event_type"], "prompt_generated");

    let (t_model_output, model_output) = ws.recv_json_timed().await;
    assert_eq!(model_output["event_type"], "model_output_generated");

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

    let first_invocation = adapter
        .first_invocation()
        .expect("adapter must have been invoked exactly once");

    // Note: we can't assert `t_prompt < first_invocation` directly — both
    // happen within nanoseconds of each other on the server (prompt_generated
    // is sent, then the model is called, with no meaningful gap between the
    // two), so a client-side receipt timestamp can never reliably beat a
    // server-side `Instant` with that little margin. The meaningful,
    // non-racy invariant is the elapsed-time-since-start check below, which
    // has the entire artificial `delay` as margin.
    assert!(
        t_model_output > first_invocation,
        "model_output_generated must be received strictly after the model adapter is invoked"
    );
    assert!(t_model_output < t_final);

    let started_to_ir = t_ir.duration_since(t_started);
    assert!(
        started_to_ir < delay,
        "pipeline_started -> ir_generated took {started_to_ir:?}, not shorter \
         than the adapter's {delay:?} artificial delay — ir_generated is being \
         held back until after the model call (spec/api.md §2.1)"
    );
    let started_to_prompt = t_prompt.duration_since(t_started);
    assert!(
        started_to_prompt < delay,
        "pipeline_started -> prompt_generated took {started_to_prompt:?}, not shorter \
         than the adapter's {delay:?} artificial delay — prompt_generated is being \
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

    let prompt = ws.recv_json().await;
    assert_eq!(prompt["event_type"], "prompt_generated");
    assert!(!prompt["data"]["prompt"]["prompt_text"]
        .as_str()
        .unwrap()
        .is_empty());

    let model_output = ws.recv_json().await;
    assert_eq!(model_output["event_type"], "model_output_generated");
    assert!(!model_output["data"]["model_output"]["raw_text"]
        .as_str()
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
    let prompt_event = ws.recv_json().await;

    let prompt = &prompt_event["data"]["prompt"];
    let prompt_text = prompt["prompt_text"].as_str().unwrap();
    assert_eq!(
        prompt_text,
        "Summarize the following text clearly and concisely:\n\narticle text"
    );
    // Load isn't a model call, so the Summarize op is IR operation index 1.
    assert_eq!(prompt["operation_index"], 1);
}

#[tokio::test]
async fn trace_multi_step_program_emits_prompt_output_pair_per_operation() {
    let delay = Duration::from_millis(50);
    let adapter = Arc::new(common::DelayedAdapter::new("model output", delay));
    let base_url = common::spawn_test_server_with_adapter(adapter.clone()).await;
    let path = common::make_temp_file("article text");
    let source =
        format!("Load the article from \"{path}\".\nSummarize it.\nTranslate it to French.");

    let (correlation_id, mut ws) = common::start_and_connect(&base_url, "/trace", &source).await;

    let (t_started, started) = ws.recv_json_timed().await;
    assert_eq!(started["event_type"], "pipeline_started");
    assert_eq!(started["correlation_id"], correlation_id);

    let raw = ws.recv_json().await;
    assert_eq!(raw["event_type"], "raw_ast_generated");

    let normalized = ws.recv_json().await;
    assert_eq!(normalized["event_type"], "normalized_ast_generated");

    let ir = ws.recv_json().await;
    assert_eq!(ir["event_type"], "ir_generated");

    // Summarize is IR operation index 1 (index 0 is Load).
    let (t_prompt_1, prompt_1) = ws.recv_json_timed().await;
    assert_eq!(prompt_1["event_type"], "prompt_generated");
    assert_eq!(prompt_1["data"]["prompt"]["operation_index"], 1);

    let (_t_output_1, output_1) = ws.recv_json_timed().await;
    assert_eq!(output_1["event_type"], "model_output_generated");
    assert_eq!(output_1["data"]["model_output"]["operation_index"], 1);

    // Translate is IR operation index 2.
    let (t_prompt_2, prompt_2) = ws.recv_json_timed().await;
    assert_eq!(prompt_2["event_type"], "prompt_generated");
    assert_eq!(prompt_2["data"]["prompt"]["operation_index"], 2);

    let (_t_output_2, output_2) = ws.recv_json_timed().await;
    assert_eq!(output_2["event_type"], "model_output_generated");
    assert_eq!(output_2["data"]["model_output"]["operation_index"], 2);

    let final_event = ws.recv_json().await;
    assert_eq!(final_event["event_type"], "final_result_ready");

    let invocations = adapter.invocations();
    assert_eq!(invocations.len(), 2, "adapter must be called exactly twice");

    // Not `t_prompt_1 < invocations[0]` directly — both happen within
    // nanoseconds of each other on the server, so a client-side receipt
    // timestamp can never reliably beat a server-side `Instant` with that
    // little margin (see the equivalent note in
    // trace_stage_events_are_emitted_incrementally_not_batched). This
    // elapsed-time check has the full `delay` as margin instead.
    let started_to_prompt_1 = t_prompt_1.duration_since(t_started);
    assert!(
        started_to_prompt_1 < delay,
        "pipeline_started -> first prompt_generated took {started_to_prompt_1:?}, not shorter \
         than the adapter's {delay:?} artificial delay"
    );
    assert!(
        t_prompt_2 >= invocations[0] + delay,
        "the second prompt_generated must not arrive until the first model call has fully \
         completed (proving the two pairs are not batched or reordered)"
    );
}
