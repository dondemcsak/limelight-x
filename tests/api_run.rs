//! Integration tests for `POST /run` — spec/bdd-api.md §2.

mod common;

use serde_json::json;

#[tokio::test]
async fn successful_run_returns_only_final_result() {
    let base_url = common::spawn_test_server("summary text").await;
    let path = common::make_temp_file("article text");
    let source = format!("Load the article from \"{path}\".\nSummarize it.");

    let (status, body) = common::post_json(&base_url, "/run", json!({ "source": source })).await;

    assert_eq!(status, 200);
    assert_eq!(body["success"], true);
    let data = &body["data"];
    assert_eq!(data["final_result"]["text"], "summary text");
    // /run's data object contains only final_result, per ui-data-contracts.md §4.
    assert_eq!(data.as_object().unwrap().len(), 1);
}

#[tokio::test]
async fn invalid_cnl_returns_structured_parse_error() {
    let base_url = common::spawn_test_server("unused").await;
    // Missing trailing period is a parse error per cnl-grammar.md §1.
    let source = "Load the article from \"article.txt\"";

    let (status, body) = common::post_json(&base_url, "/run", json!({ "source": source })).await;

    assert_eq!(status, 200);
    assert_eq!(body["success"], false);
    let error = &body["errors"][0];
    assert_eq!(error["code"], "ERR_CNL_PARSE");
    assert_eq!(error["category"], "pipeline");
    assert!(error["location"]["line"].is_number());
}

#[tokio::test]
async fn evaluator_fatal_error_reports_operation_index() {
    let base_url = common::spawn_test_server("unused").await;
    let source = "Load the article from \"missing_file_abc123.txt\".";

    let (status, body) = common::post_json(&base_url, "/run", json!({ "source": source })).await;

    assert_eq!(status, 200);
    assert_eq!(body["success"], false);
    let error = &body["errors"][0];
    assert_eq!(error["code"], "ERR_EVALUATOR_FATAL");
    assert_eq!(error["severity"], "fatal");
    let message = error["message"].as_str().unwrap();
    assert!(message.contains("operation 0"), "unexpected message: {message}");
    assert!(message.contains("missing_file_abc123.txt"), "unexpected message: {message}");
}
