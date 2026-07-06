//! Integration tests for `POST /run` — spec/bdd-api.md §2.

mod common;

#[tokio::test]
async fn successful_run_streams_started_then_final_result() {
    let base_url = common::spawn_test_server("summary text").await;
    let path = common::make_temp_file("article text");
    let source = format!("Load the article from \"{path}\".\nSummarize it.");

    let (correlation_id, mut ws) = common::start_and_connect(&base_url, "/run", &source).await;

    let started = ws.recv_json().await;
    assert_eq!(started["event_type"], "pipeline_started");
    assert_eq!(started["correlation_id"], correlation_id);

    let final_event = ws.recv_json().await;
    assert_eq!(final_event["event_type"], "final_result_ready");
    assert_eq!(final_event["correlation_id"], correlation_id);
    assert_eq!(final_event["success"], true);
    let data = &final_event["data"];
    assert_eq!(data["final_result"]["text"], "summary text");
    // /run's data object contains only final_result, per ui-data-contracts.md §9.
    assert_eq!(data.as_object().unwrap().len(), 1);
}

#[tokio::test]
async fn invalid_cnl_streams_pipeline_failed_with_parse_error() {
    let base_url = common::spawn_test_server("unused").await;
    // Missing trailing period is a parse error per cnl-grammar.md §1.
    let source = "Load the article from \"article.txt\"";

    let (correlation_id, mut ws) = common::start_and_connect(&base_url, "/run", source).await;

    let started = ws.recv_json().await;
    assert_eq!(started["event_type"], "pipeline_started");

    let failed = ws.recv_json().await;
    assert_eq!(failed["event_type"], "pipeline_failed");
    assert_eq!(failed["correlation_id"], correlation_id);
    assert_eq!(failed["success"], false);
    let error = &failed["errors"][0];
    assert_eq!(error["code"], "ERR_CNL_PARSE");
    assert_eq!(error["category"], "pipeline");
    assert!(error["location"]["line"].is_number());
}

#[tokio::test]
async fn evaluator_fatal_error_reports_operation_index() {
    let base_url = common::spawn_test_server("unused").await;
    let source = "Load the article from \"missing_file_abc123.txt\".";

    let (_correlation_id, mut ws) = common::start_and_connect(&base_url, "/run", source).await;
    let _started = ws.recv_json().await;
    let failed = ws.recv_json().await;

    assert_eq!(failed["event_type"], "pipeline_failed");
    let error = &failed["errors"][0];
    assert_eq!(error["code"], "ERR_EVALUATOR_FATAL");
    assert_eq!(error["severity"], "fatal");
    let message = error["message"].as_str().unwrap();
    assert!(
        message.contains("operation 0"),
        "unexpected message: {message}"
    );
    assert!(
        message.contains("missing_file_abc123.txt"),
        "unexpected message: {message}"
    );
}
