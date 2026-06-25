/// An input reference in the raw AST — may be a pronoun, named variable, or resource noun phrase.
#[derive(Debug, Clone, PartialEq)]
pub enum RawInput {
    /// A pronoun such as `it`, `them`, `the result`, etc.
    Pronoun(String),
    /// A bare identifier bound via a `Let` statement (e.g., `summary`).
    Name(String),
    /// A noun phrase referencing a loaded resource (e.g., `the article`).
    Resource(String),
}

/// The value side of a `Let <name> be <expression>` binding.
#[derive(Debug, Clone, PartialEq)]
pub enum Expression {
    Pronoun(String),
    Name(String),
}

/// A single node in the raw (pre-normalization) AST.
#[derive(Debug, Clone, PartialEq)]
pub enum RawNode {
    Load {
        resource: String,
        path: String,
    },
    Extract {
        target: String,
        /// `None` when no `from <input>` clause is present (implicit previous result).
        input: Option<RawInput>,
    },
    Summarize {
        input: RawInput,
        prompt: Option<String>,
    },
    Translate {
        input: RawInput,
        language: String,
        prompt: Option<String>,
    },
    Rewrite {
        input: RawInput,
        prompt: Option<String>,
    },
    Format {
        input: RawInput,
        target: String,
    },
    Bind {
        name: String,
        value: Expression,
    },
    /// Internal-only: `Let <name> be the <resource> from "<path>"`.
    /// Expanded by the normalizer into a Load + symbol-table entry.
    BindLoad {
        name: String,
        resource: String,
        path: String,
    },
}

/// The complete raw AST produced by the parser.
#[derive(Debug, Clone, PartialEq)]
pub struct RawAst(pub Vec<RawNode>);
