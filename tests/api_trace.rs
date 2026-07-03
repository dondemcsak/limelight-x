//! Integration tests for `POST /trace` — spec/bdd-api.md §4.

mod common;

use serde_json::json;

#[tokio::test]
async fn successful_trace_returns_full_pipeline_output() {
    let base_url = common::spawn_test_server("summary text").await;
    let path = common::make_temp_file("article text");
    let source = format!("Load the article from \"{path}\".\nSummarize it.");

    let (status, body) = common::post_json(&base_url, "/trace", json!({ "source": source })).await;

    assert_eq!(status, 200);
    assert_eq!(body["success"], true);
    let data = body["data"].as_object().unwrap();
    for key in [
        "raw_ast",
        "normalized_ast",
        "ir",
        "prompts",
        "model_outputs",
    ] {
        assert!(data.contains_key(key), "missing key: {key}");
    }
    assert!(!body["data"]["ir"]["operations"]
        .as_array()
        .unwrap()
        .is_empty());
    assert!(!body["data"]["prompts"].as_array().unwrap().is_empty());
    assert!(!body["data"]["model_outputs"].as_array().unwrap().is_empty());
}

#[tokio::test]
async fn trace_prompts_match_evaluator_constructed_prompts_exactly() {
    let base_url = common::spawn_test_server("summary text").await;
    let path = common::make_temp_file("article text");
    // No custom prompt -> the built-in Summarize template, per evaluator-semantics.md.
    let source = format!("Load the article from \"{path}\".\nSummarize it.");

    let (_status, body) = common::post_json(&base_url, "/trace", json!({ "source": source })).await;

    let prompts = body["data"]["prompts"].as_array().unwrap();
    // Load isn't a model call, so only the Summarize op produces a prompt block.
    assert_eq!(prompts.len(), 1);
    let prompt_text = prompts[0]["prompt_text"].as_str().unwrap();
    assert_eq!(
        prompt_text,
        "Summarize the following text clearly and concisely:\n\narticle text"
    );
    assert_eq!(prompts[0]["operation_index"], 1);
}
