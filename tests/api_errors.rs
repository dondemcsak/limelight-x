//! Integration tests for the shared error response shape — spec/bdd-api.md §5.

mod common;

#[tokio::test]
async fn malformed_json_body_returns_structured_error() {
    let base_url = common::spawn_test_server("unused").await;

    let (status, body) = common::post_raw(&base_url, "/run", "{not valid json").await;

    assert_eq!(status, 400);
    assert_eq!(body["version"], "v1");
    assert_eq!(body["success"], false);
    assert_eq!(body["errors"][0]["code"], "ERR_MALFORMED_REQUEST");
    assert_eq!(body["errors"][0]["category"], "api");
}

#[tokio::test]
async fn missing_required_field_returns_structured_error() {
    let base_url = common::spawn_test_server("unused").await;

    let (status, body) = common::post_raw(&base_url, "/run", "{}").await;

    assert_eq!(status, 400);
    assert_eq!(body["success"], false);
    assert_eq!(body["errors"][0]["code"], "ERR_MISSING_FIELD");
    let message = body["errors"][0]["message"].as_str().unwrap();
    assert!(
        message.contains("'source'"),
        "unexpected message: {message}"
    );
}

#[tokio::test]
async fn missing_required_field_check_applies_to_explain_and_trace_too() {
    let base_url = common::spawn_test_server("unused").await;

    for path in ["/explain", "/trace"] {
        let (status, body) = common::post_raw(&base_url, path, "{}").await;
        assert_eq!(status, 400, "path: {path}");
        assert_eq!(
            body["errors"][0]["code"], "ERR_MISSING_FIELD",
            "path: {path}"
        );
    }
}
