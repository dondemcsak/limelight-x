//! Integration tests for `POST /explain` — spec/bdd-api.md §3.

mod common;

#[tokio::test]
async fn successful_explain_streams_raw_and_normalized_ast_only() {
    // Distinctive response text makes it easy to assert no model call occurred.
    let base_url = common::spawn_test_server("__SHOULD_NOT_BE_CALLED__").await;
    let source = "Load the article from \"article.txt\".\nSummarize it.";

    let (correlation_id, mut ws) = common::start_and_connect(&base_url, "/explain", source).await;

    let started = ws.recv_json().await;
    assert_eq!(started["event_type"], "pipeline_started");
    assert_eq!(started["correlation_id"], correlation_id);

    let raw = ws.recv_json().await;
    assert_eq!(raw["event_type"], "raw_ast_generated");
    assert_eq!(raw["correlation_id"], correlation_id);
    let raw_root = &raw["data"]["raw_ast"]["root"];
    assert_eq!(raw_root["type"], "Program");
    assert_eq!(raw_root["children"][0]["type"], "Load");
    assert_eq!(raw_root["children"][1]["type"], "Summarize");
    // Pronoun is unresolved in the raw AST ("it").
    assert_eq!(raw_root["children"][1]["metadata"]["pronoun"], "it");

    let normalized = ws.recv_json().await;
    assert_eq!(normalized["event_type"], "normalized_ast_generated");
    let normalized_root = &normalized["data"]["normalized_ast"]["root"];
    assert_eq!(normalized_root["children"][1]["type"], "Summarize");
    // Normalized AST never carries pronouns.
    assert!(normalized_root["children"][1]["metadata"]["pronoun"].is_null());

    // /explain never evaluates: no further events (ir/prompts/model_outputs/
    // final_result) are ever emitted for this endpoint (spec/api.md §2.1).
}

#[tokio::test]
async fn explain_streams_normalization_error_without_evaluating() {
    let base_url = common::spawn_test_server("__SHOULD_NOT_BE_CALLED__").await;
    // No prior statement — "it" cannot be resolved.
    let source = "Summarize it.";

    let (correlation_id, mut ws) = common::start_and_connect(&base_url, "/explain", source).await;
    let started = ws.recv_json().await;
    assert_eq!(started["event_type"], "pipeline_started");

    let failed = ws.recv_json().await;
    assert_eq!(failed["event_type"], "pipeline_failed");
    assert_eq!(failed["correlation_id"], correlation_id);
    assert_eq!(failed["success"], false);
    let error = &failed["errors"][0];
    assert_eq!(error["code"], "ERR_CNL_NORMALIZE");
    let message = error["message"].as_str().unwrap();
    assert!(
        message.contains("No prior result for pronoun")
            || message.contains("'it'")
            || message.contains("it"),
        "unexpected message: {message}"
    );
}
