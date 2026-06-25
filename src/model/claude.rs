use reqwest::blocking::Client;
use serde::Deserialize;
use serde_json::json;

use super::ModelAdapter;
use crate::error::Error;

const MODEL_ID: &str = "claude-sonnet-4-6";
const API_ENDPOINT: &str = "https://api.anthropic.com/v1/messages";
const ANTHROPIC_VERSION: &str = "2023-06-01";
const MAX_TOKENS: u32 = 2048;

/// Concrete model adapter that calls the Anthropic Claude 3.5 Sonnet Messages API.
pub struct ClaudeModelAdapter {
    api_key: String,
    client: Client,
}

impl ClaudeModelAdapter {
    /// Construct a new adapter, reading the API key from `ANTHROPIC_API_KEY`.
    pub fn new() -> Result<Self, Error> {
        let api_key = std::env::var("ANTHROPIC_API_KEY").map_err(|_| Error::MissingApiKey)?;
        let client = Client::new();
        Ok(Self { api_key, client })
    }
}

// ---------------------------------------------------------------------------
// Response deserialization
// ---------------------------------------------------------------------------

#[derive(Deserialize)]
struct ContentBlock {
    text: String,
}

#[derive(Deserialize)]
struct MessageResponse {
    content: Vec<ContentBlock>,
}

// ---------------------------------------------------------------------------
// ModelAdapter implementation
// ---------------------------------------------------------------------------

impl ModelAdapter for ClaudeModelAdapter {
    fn complete(&self, prompt: &str) -> Result<String, Error> {
        let body = json!({
            "model": MODEL_ID,
            "max_tokens": MAX_TOKENS,
            "temperature": 0.0,
            "messages": [
                { "role": "user", "content": prompt }
            ]
        });

        let response = self
            .client
            .post(API_ENDPOINT)
            .header("x-api-key", &self.api_key)
            .header("anthropic-version", ANTHROPIC_VERSION)
            .header("content-type", "application/json")
            .json(&body)
            .send()
            .map_err(|e| Error::ModelAdapterNetworkError(e.to_string()))?;

        let status = response.status().as_u16();
        if status != 200 {
            let body_text = response.text().unwrap_or_default();
            return Err(Error::ModelAdapterHttpError(status, body_text));
        }

        let parsed: MessageResponse = response
            .json()
            .map_err(|e| Error::ModelAdapterInvalidResponse(e.to_string()))?;

        parsed
            .content
            .into_iter()
            .next()
            .map(|block| block.text)
            .ok_or_else(|| {
                Error::ModelAdapterMalformedResponse(
                    "response.content[0].text is missing".to_string(),
                )
            })
    }
}
