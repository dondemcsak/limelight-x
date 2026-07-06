//! Integration tests for the WebSocket streaming transport itself
//! (spec/api.md §2.1-2.3): `pipeline_failed` short-circuiting a sequence,
//! and reconnect ("last connection wins").

mod common;

use serde_json::json;

#[tokio::test]
async fn pipeline_failed_short_circuits_trace_sequence() {
    let base_url = common::spawn_test_server("unused").await;
    // No prior statement — "it" cannot be resolved, so normalization fails
    // well before IR/prompts/model_outputs would ever be produced.
    let source = "Summarize it.";

    let (correlation_id, mut ws) = common::start_and_connect(&base_url, "/trace", source).await;

    let started = ws.recv_json().await;
    assert_eq!(started["event_type"], "pipeline_started");

    let failed = ws.recv_json().await;
    assert_eq!(failed["event_type"], "pipeline_failed");
    assert_eq!(failed["correlation_id"], correlation_id);
    assert_eq!(failed["errors"][0]["code"], "ERR_CNL_NORMALIZE");
}

#[tokio::test]
async fn reconnecting_client_becomes_the_sole_receiver() {
    let base_url = common::spawn_test_server("summary text").await;
    let path = common::make_temp_file("article text");
    let source = format!("Load the article from \"{path}\".\nSummarize it.");

    // The first connection is immediately superseded by the second — "last
    // connection wins" (spec/api.md §2.3). We don't assert anything about
    // `_first` receiving nothing, since proving a negative without a
    // WS-client-side timeout mechanism would be racy; the meaningful
    // contract under test is that the *newest* connection receives events.
    let _first = common::TestWsClient::connect(&base_url).await;
    let mut second = common::TestWsClient::connect(&base_url).await;

    let (status, ack) = common::post_json(&base_url, "/run", json!({ "source": source })).await;
    assert_eq!(status, 200);
    let correlation_id = ack["correlation_id"].as_str().unwrap().to_string();

    let started = second.recv_json().await;
    assert_eq!(started["event_type"], "pipeline_started");
    assert_eq!(started["correlation_id"], correlation_id);

    let final_event = second.recv_json().await;
    assert_eq!(final_event["event_type"], "final_result_ready");
    assert_eq!(final_event["data"]["final_result"]["text"], "summary text");
}
