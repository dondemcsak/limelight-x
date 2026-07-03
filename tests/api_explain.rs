//! Integration tests for `POST /explain` — spec/bdd-api.md §3.

mod common;

use serde_json::json;

#[tokio::test]
async fn successful_explain_returns_raw_and_normalized_ast_only() {
    // Distinctive response text makes it easy to assert no model call occurred.
    let base_url = common::spawn_test_server("__SHOULD_NOT_BE_CALLED__").await;
    let source = "Load the article from \"article.txt\".\nSummarize it.";

    let (status, body) =
        common::post_json(&base_url, "/explain", json!({ "source": source })).await;

    assert_eq!(status, 200);
    assert_eq!(body["success"], true);
    let data = body["data"].as_object().unwrap();
    assert!(data.contains_key("raw_ast"));
    assert!(data.contains_key("normalized_ast"));
    // No model_outputs/prompts/final_result — /explain never evaluates.
    assert!(!data.contains_key("prompts"));
    assert!(!data.contains_key("model_outputs"));
    assert!(!data.contains_key("final_result"));

    let raw_root = &body["data"]["raw_ast"]["root"];
    assert_eq!(raw_root["type"], "Program");
    assert_eq!(raw_root["children"][0]["type"], "Load");
    assert_eq!(raw_root["children"][1]["type"], "Summarize");
    // Pronoun is unresolved in the raw AST ("it").
    assert_eq!(raw_root["children"][1]["metadata"]["pronoun"], "it");

    let normalized_root = &body["data"]["normalized_ast"]["root"];
    assert_eq!(normalized_root["children"][1]["type"], "Summarize");
    // Normalized AST never carries pronouns.
    assert!(normalized_root["children"][1]["metadata"]["pronoun"].is_null());
}

#[tokio::test]
async fn explain_surfaces_normalization_errors_without_evaluating() {
    let base_url = common::spawn_test_server("__SHOULD_NOT_BE_CALLED__").await;
    // No prior statement — "it" cannot be resolved.
    let source = "Summarize it.";

    let (status, body) =
        common::post_json(&base_url, "/explain", json!({ "source": source })).await;

    assert_eq!(status, 200);
    assert_eq!(body["success"], false);
    let error = &body["errors"][0];
    assert_eq!(error["code"], "ERR_CNL_NORMALIZE");
    let message = error["message"].as_str().unwrap();
    assert!(
        message.contains("No prior result for pronoun")
            || message.contains("'it'")
            || message.contains("it"),
        "unexpected message: {message}"
    );
}
