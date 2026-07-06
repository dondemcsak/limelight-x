//! Integration tests for `POST /trace` — spec/bdd-api.md §4.

mod common;

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
