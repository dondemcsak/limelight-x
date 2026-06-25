pub mod claude;
#[cfg(test)]
pub mod mock;

use crate::error::Error;

/// The single interface the evaluator uses to call the language model.
pub trait ModelAdapter {
    fn complete(&self, prompt: &str) -> Result<String, Error>;
}
