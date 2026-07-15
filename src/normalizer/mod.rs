pub mod ast;

use std::collections::HashMap;

use ast::{InputRef, NormalizedAst, NormalizedNode};

use crate::error::Error;
use crate::parser::ast::{Expression, RawAst, RawInput, RawNode};

// ---------------------------------------------------------------------------
// Public entry point
// ---------------------------------------------------------------------------

/// Normalize a raw AST into a canonical, fully-explicit AST.
pub fn normalize(raw: &RawAst) -> Result<NormalizedAst, Error> {
    let mut ctx = NormContext::new();
    let mut nodes: Vec<NormalizedNode> = Vec::new();

    for raw_node in &raw.0 {
        if let Some(node) = ctx.normalize_node(raw_node)? {
            nodes.push(node);
        }
    }

    Ok(NormalizedAst(nodes))
}

// ---------------------------------------------------------------------------
// Normalization context
// ---------------------------------------------------------------------------

struct NormContext {
    /// Maps variable names introduced by `Let` to their resolved `InputRef`.
    symbol_table: HashMap<String, InputRef>,
    /// Whether at least one statement has produced a result (for PreviousResult checks).
    has_previous_result: bool,
}

impl NormContext {
    fn new() -> Self {
        Self {
            symbol_table: HashMap::new(),
            has_previous_result: false,
        }
    }

    /// Normalize one raw node.
    /// Returns `None` for `Bind`/`BindLoad` nodes (they update the symbol table only).
    fn normalize_node(&mut self, node: &RawNode) -> Result<Option<NormalizedNode>, Error> {
        match node {
            RawNode::Load { resource, path } => {
                let normalized = NormalizedNode::Load {
                    resource: resource.clone(),
                    path: path.clone(),
                };
                self.has_previous_result = true;
                Ok(Some(normalized))
            }

            RawNode::Extract { target, input } => {
                let input_ref = self.resolve_optional_input(input.as_ref())?;
                self.has_previous_result = true;
                Ok(Some(NormalizedNode::Extract {
                    target: target.clone(),
                    input: input_ref,
                }))
            }

            RawNode::Summarize { input, prompt } => {
                let input_ref = self.resolve_input(input)?;
                self.has_previous_result = true;
                Ok(Some(NormalizedNode::Summarize {
                    input: input_ref,
                    prompt: prompt.clone(),
                }))
            }

            RawNode::Translate {
                input,
                language,
                prompt,
            } => {
                let input_ref = self.resolve_input(input)?;
                self.has_previous_result = true;
                Ok(Some(NormalizedNode::Translate {
                    input: input_ref,
                    language: language.clone(),
                    prompt: prompt.clone(),
                }))
            }

            RawNode::Rewrite { input, prompt } => {
                let input_ref = self.resolve_input(input)?;
                self.has_previous_result = true;
                Ok(Some(NormalizedNode::Rewrite {
                    input: input_ref,
                    prompt: prompt.clone(),
                }))
            }

            RawNode::Format { input, target } => {
                let input_ref = self.resolve_input(input)?;
                self.has_previous_result = true;
                Ok(Some(NormalizedNode::Format {
                    input: input_ref,
                    target: target.clone(),
                }))
            }

            RawNode::Bind { name, value } => {
                let input_ref = self.resolve_expression(value)?;
                self.bind(name.clone(), input_ref)?;
                Ok(None) // Bind produces no IR node
            }

            RawNode::BindLoad {
                name,
                resource,
                path,
            } => {
                // Expands to: Load (implicit) + symbol table entry pointing to Resource
                // We emit the Load node and bind the name to the resource.
                let load_node = NormalizedNode::Load {
                    resource: resource.clone(),
                    path: path.clone(),
                };
                self.has_previous_result = true;
                self.bind(name.clone(), InputRef::Resource(resource.clone()))?;
                Ok(Some(load_node))
            }
        }
    }

    /// Resolve an explicit `RawInput` to an `InputRef`.
    fn resolve_input(&self, raw: &RawInput) -> Result<InputRef, Error> {
        match raw {
            RawInput::Pronoun(p) => {
                if !self.has_previous_result {
                    return Err(Error::NormalizeError(format!(
                        "No prior result for pronoun '{p}'"
                    )));
                }
                Ok(InputRef::PreviousResult)
            }
            RawInput::Name(n) => self.lookup_name(n),
            RawInput::Resource(r) => Ok(InputRef::Resource(r.clone())),
        }
    }

    /// Resolve an optional input (for `Extract` which may have implicit input).
    fn resolve_optional_input(&self, raw: Option<&RawInput>) -> Result<InputRef, Error> {
        match raw {
            Some(input) => self.resolve_input(input),
            None => {
                if !self.has_previous_result {
                    return Err(Error::NormalizeError(
                        "No prior result for implicit input".to_string(),
                    ));
                }
                Ok(InputRef::PreviousResult)
            }
        }
    }

    /// Resolve an `Expression` (from a `Let` binding value).
    fn resolve_expression(&self, expr: &Expression) -> Result<InputRef, Error> {
        match expr {
            Expression::Pronoun(p) => {
                if !self.has_previous_result {
                    return Err(Error::NormalizeError(format!(
                        "No prior result for pronoun '{p}'"
                    )));
                }
                Ok(InputRef::PreviousResult)
            }
            Expression::Name(n) => self.lookup_name(n),
        }
    }

    /// Look up a named variable in the symbol table.
    fn lookup_name(&self, name: &str) -> Result<InputRef, Error> {
        self.symbol_table
            .get(name)
            .cloned()
            .ok_or_else(|| Error::NormalizeError(format!("Unknown variable '{name}'")))
    }

    /// Add a name → InputRef mapping to the symbol table.
    /// Shadowing (rebinding an existing name) is a fatal error.
    fn bind(&mut self, name: String, value: InputRef) -> Result<(), Error> {
        if self.symbol_table.contains_key(&name) {
            return Err(Error::NormalizeError(format!(
                "variable '{name}' is already bound (shadowing not allowed)"
            )));
        }
        self.symbol_table.insert(name, value);
        Ok(())
    }
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

#[cfg(test)]
mod tests {
    use super::*;
    use crate::parser;
    use ast::{InputRef, NormalizedNode};

    fn parse_and_normalize(src: &str) -> NormalizedAst {
        let raw = parser::parse(src).unwrap();
        normalize(&raw).unwrap()
    }

    fn parse_and_normalize_err(src: &str) -> Error {
        let raw = parser::parse(src).unwrap();
        normalize(&raw).unwrap_err()
    }

    // BDD: Pronoun resolution
    #[test]
    fn test_pronoun_resolution() {
        // GIVEN Load + Summarize(input = Pronoun("it"))
        // WHEN the normalizer runs
        // THEN input = PreviousResult, no NamedVariable nodes
        let ast = parse_and_normalize("Load the article from \"article.txt\".\nSummarize it.");
        assert_eq!(
            ast.0[1],
            NormalizedNode::Summarize {
                input: InputRef::PreviousResult,
                prompt: None,
            }
        );
    }

    // BDD: Implicit input resolution
    #[test]
    fn test_implicit_input_resolution() {
        // GIVEN Load + Extract(target="entities", input=None)
        // WHEN the normalizer runs
        // THEN input = PreviousResult
        let ast =
            parse_and_normalize("Load the article from \"article.txt\".\nExtract the entities.");
        assert_eq!(
            ast.0[1],
            NormalizedNode::Extract {
                target: "entities".to_string(),
                input: InputRef::PreviousResult,
            }
        );
    }

    // BDD: Variable binding resolution
    #[test]
    fn test_variable_binding_resolution() {
        // GIVEN: Load, Let summary be the result., Summarize summary.
        // WHEN the normalizer runs
        // THEN: Summarize gets input = PreviousResult (fully resolved), no NamedVariable
        let src =
            "Load the article from \"article.txt\".\nLet saved be the result.\nSummarize saved.";
        let ast = parse_and_normalize(src);
        // Load at 0, Summarize at 1 (Bind is removed)
        assert_eq!(ast.0.len(), 2);
        assert_eq!(
            ast.0[1],
            NormalizedNode::Summarize {
                input: InputRef::PreviousResult,
                prompt: None,
            }
        );
    }

    // BDD: Pronoun resolution failure
    #[test]
    fn test_pronoun_resolution_failure() {
        // GIVEN Summarize it. with no prior result
        // WHEN the normalizer runs
        // THEN fatal error: "No prior result for pronoun 'it'"
        let err = parse_and_normalize_err("Summarize it.");
        let msg = err.to_string();
        assert!(msg.contains("No prior result"), "unexpected error: {msg}");
    }

    // BDD: Unknown variable name
    #[test]
    fn test_unknown_variable_error() {
        // GIVEN Summarize summary. without a prior binding
        // WHEN the normalizer runs
        // THEN fatal error: "Unknown variable 'summary'"
        let err = parse_and_normalize_err("Summarize summary.");
        let msg = err.to_string();
        assert!(msg.contains("Unknown variable"), "unexpected error: {msg}");
    }

    #[test]
    fn test_bind_load_pattern() {
        // Let article be the text from "article.txt". should produce a Load node
        // and bind "article" to Resource("text").
        let src =
            "Let article be the text from \"article.txt\".\nExtract the entities from article.";
        let ast = parse_and_normalize(src);
        assert_eq!(ast.0.len(), 2);
        assert_eq!(
            ast.0[0],
            NormalizedNode::Load {
                resource: "text".to_string(),
                path: "article.txt".to_string(),
            }
        );
        assert_eq!(
            ast.0[1],
            NormalizedNode::Extract {
                target: "entities".to_string(),
                input: InputRef::Resource("text".to_string()),
            }
        );
    }

    #[test]
    fn test_no_shadowing() {
        let src =
            "Load the article from \"article.txt\".\nLet x be the result.\nLet x be the result.";
        let raw = parser::parse(src).unwrap();
        let err = normalize(&raw).unwrap_err();
        assert!(err.to_string().contains("already bound"));
    }
}
