/// A fully resolved input reference — no pronouns or named variables remain.
#[derive(Debug, Clone, PartialEq)]
pub enum InputRef {
    /// Refers to the output of the immediately preceding operation.
    PreviousResult,
    /// Refers to a resource loaded by a `Load` statement (e.g., `"article"`).
    Resource(String),
}

/// A single node in the normalized AST.
/// All inputs are explicit; no `Bind` or pronoun nodes remain.
#[derive(Debug, Clone, PartialEq)]
pub enum NormalizedNode {
    Load {
        resource: String,
        path: String,
    },
    Extract {
        target: String,
        input: InputRef,
    },
    Summarize {
        input: InputRef,
        prompt: Option<String>,
    },
    Translate {
        input: InputRef,
        language: String,
        prompt: Option<String>,
    },
    Rewrite {
        input: InputRef,
        prompt: Option<String>,
    },
    Format {
        input: InputRef,
        target: String,
    },
}

/// The complete normalized AST, ready for IR compilation.
#[derive(Debug, Clone, PartialEq)]
pub struct NormalizedAst(pub Vec<NormalizedNode>);
