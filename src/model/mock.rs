use super::ModelAdapter;
use crate::error::Error;

/// A test double for [`ModelAdapter`] that returns a fixed string.
pub struct MockModelAdapter {
    pub response: String,
}

impl MockModelAdapter {
    pub fn new(response: impl Into<String>) -> Self {
        Self {
            response: response.into(),
        }
    }
}

impl ModelAdapter for MockModelAdapter {
    fn complete(&self, _prompt: &str) -> Result<String, Error> {
        Ok(self.response.clone())
    }
}

/// A test double that also captures the last prompt it received.
pub struct CapturingAdapter {
    pub response: String,
    pub last_prompt: std::cell::RefCell<Option<String>>,
}

impl CapturingAdapter {
    pub fn new(response: impl Into<String>) -> Self {
        Self {
            response: response.into(),
            last_prompt: std::cell::RefCell::new(None),
        }
    }

    pub fn last_prompt(&self) -> Option<String> {
        self.last_prompt.borrow().clone()
    }
}

impl ModelAdapter for CapturingAdapter {
    fn complete(&self, prompt: &str) -> Result<String, Error> {
        *self.last_prompt.borrow_mut() = Some(prompt.to_string());
        Ok(self.response.clone())
    }
}
