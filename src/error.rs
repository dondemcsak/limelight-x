use thiserror::Error;

/// Crate-wide error type for all Limelight-X pipeline stages.
#[derive(Debug, Error)]
pub enum Error {
    #[error("parse error at line {line}, column {column}: {message}")]
    ParseError {
        line: usize,
        column: usize,
        message: String,
    },

    #[error("normalize error: {0}")]
    NormalizeError(String),

    #[error("IR error: {0}")]
    IrError(String),

    #[error("eval error at operation {index} ({op}): {message}")]
    EvalError {
        index: usize,
        op: String,
        message: String,
    },

    #[error("model adapter network error: {0}")]
    ModelAdapterNetworkError(String),

    #[error("model adapter invalid response: {0}")]
    ModelAdapterInvalidResponse(String),

    #[error("model adapter malformed response: {0}")]
    ModelAdapterMalformedResponse(String),

    #[error("model adapter HTTP error {0}: {1}")]
    ModelAdapterHttpError(u16, String),

    #[error("missing environment variable: ANTHROPIC_API_KEY")]
    MissingApiKey,

    #[error("I/O error: {0}")]
    IoError(#[from] std::io::Error),
}
