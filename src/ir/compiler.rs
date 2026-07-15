use std::collections::HashMap;

use super::op::{Ir, IrOp, IrRef};
use crate::error::Error;
use crate::normalizer::ast::{InputRef, NormalizedAst, NormalizedNode};

// ---------------------------------------------------------------------------
// Public entry point
// ---------------------------------------------------------------------------

/// Compile a normalized AST into a linear IR.
pub fn compile(ast: &NormalizedAst) -> Result<Ir, Error> {
    if ast.0.is_empty() {
        return Err(Error::IrError("IR program is empty".to_string()));
    }

    let mut ops: Vec<IrOp> = Vec::new();
    // Maps resource names to the IR operation index that produced them.
    let mut resource_index: HashMap<String, usize> = HashMap::new();
    let mut last_index: Option<usize> = None;

    for node in &ast.0 {
        let current_index = ops.len();

        let op = match node {
            NormalizedNode::Load { resource, path } => {
                resource_index.insert(resource.clone(), current_index);
                IrOp::Load { path: path.clone() }
            }

            NormalizedNode::Extract { target, input } => {
                let ir_ref = resolve(input, last_index, &resource_index)?;
                resource_index.insert(target.clone(), current_index);
                IrOp::Extract {
                    target: target.clone(),
                    input: ir_ref,
                }
            }

            NormalizedNode::Summarize { input, prompt } => {
                let ir_ref = resolve(input, last_index, &resource_index)?;
                resource_index.insert("summary".to_string(), current_index);
                IrOp::Summarize {
                    input: ir_ref,
                    prompt: prompt.clone(),
                }
            }

            NormalizedNode::Translate {
                input,
                language,
                prompt,
            } => {
                let ir_ref = resolve(input, last_index, &resource_index)?;
                IrOp::Translate {
                    input: ir_ref,
                    language: language.clone(),
                    prompt: prompt.clone(),
                }
            }

            NormalizedNode::Rewrite { input, prompt } => {
                let ir_ref = resolve(input, last_index, &resource_index)?;
                if let Some(input_name) = reverse_lookup(&resource_index, ir_ref.0) {
                    resource_index.insert(format!("rewritten {input_name}"), current_index);
                }
                IrOp::Rewrite {
                    input: ir_ref,
                    prompt: prompt.clone(),
                }
            }

            NormalizedNode::Format { input, target } => {
                let ir_ref = resolve(input, last_index, &resource_index)?;
                IrOp::Format {
                    input: ir_ref,
                    target: target.clone(),
                }
            }
        };

        ops.push(op);
        last_index = Some(current_index);
    }

    Ok(Ir(ops))
}

// ---------------------------------------------------------------------------
// InputRef → IrRef resolution
// ---------------------------------------------------------------------------

fn reverse_lookup(resource_index: &HashMap<String, usize>, idx: usize) -> Option<String> {
    resource_index
        .iter()
        .find(|(_, &v)| v == idx)
        .map(|(k, _)| k.clone())
}

fn resolve(
    input: &InputRef,
    last_index: Option<usize>,
    resource_index: &HashMap<String, usize>,
) -> Result<IrRef, Error> {
    match input {
        InputRef::PreviousResult => {
            let idx = last_index.ok_or_else(|| {
                Error::IrError("PreviousResult referenced before any operation".to_string())
            })?;
            Ok(IrRef(idx))
        }
        InputRef::Resource(name) => {
            let idx = resource_index
                .get(name)
                .copied()
                .ok_or_else(|| Error::IrError(format!("undefined resource reference '{name}'")))?;
            Ok(IrRef(idx))
        }
    }
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

#[cfg(test)]
mod tests {
    use super::*;
    use crate::normalizer::ast::{InputRef, NormalizedAst, NormalizedNode};

    fn make_ast(nodes: Vec<NormalizedNode>) -> NormalizedAst {
        NormalizedAst(nodes)
    }

    // BDD: Compile Load + Summarize
    #[test]
    fn test_compile_load_summarize() {
        // GIVEN: Load(path="article.txt"), Summarize(input=PreviousResult, prompt=None)
        // WHEN IR compiler runs
        // THEN: [Load { path }, Summarize { input: $0, prompt: None }]
        let ast = make_ast(vec![
            NormalizedNode::Load {
                resource: "article".to_string(),
                path: "article.txt".to_string(),
            },
            NormalizedNode::Summarize {
                input: InputRef::PreviousResult,
                prompt: None,
            },
        ]);
        let ir = compile(&ast).unwrap();
        assert_eq!(
            ir.0[0],
            IrOp::Load {
                path: "article.txt".to_string()
            }
        );
        assert_eq!(
            ir.0[1],
            IrOp::Summarize {
                input: IrRef(0),
                prompt: None,
            }
        );
    }

    // BDD: Compile Summarize with expression hole
    #[test]
    fn test_compile_summarize_with_prompt() {
        // GIVEN a Summarize node with custom prompt
        // WHEN IR compiler runs
        // THEN prompt is embedded verbatim
        let ast = make_ast(vec![
            NormalizedNode::Load {
                resource: "article".to_string(),
                path: "article.txt".to_string(),
            },
            NormalizedNode::Summarize {
                input: InputRef::PreviousResult,
                prompt: Some("Summarize in 3 bullets.".to_string()),
            },
        ]);
        let ir = compile(&ast).unwrap();
        assert_eq!(
            ir.0[1],
            IrOp::Summarize {
                input: IrRef(0),
                prompt: Some("Summarize in 3 bullets.".to_string()),
            }
        );
    }

    // BDD: Compile Extract + Summarize chain
    #[test]
    fn test_compile_extract_summarize_chain() {
        // GIVEN: Load → Extract → Summarize
        // WHEN IR compiler runs
        // THEN: $0, $1, $2 references are correct
        let ast = make_ast(vec![
            NormalizedNode::Load {
                resource: "article".to_string(),
                path: "article.txt".to_string(),
            },
            NormalizedNode::Extract {
                target: "entities".to_string(),
                input: InputRef::PreviousResult,
            },
            NormalizedNode::Summarize {
                input: InputRef::PreviousResult,
                prompt: None,
            },
        ]);
        let ir = compile(&ast).unwrap();
        assert_eq!(
            ir.0[1],
            IrOp::Extract {
                target: "entities".to_string(),
                input: IrRef(0)
            }
        );
        assert_eq!(
            ir.0[2],
            IrOp::Summarize {
                input: IrRef(1),
                prompt: None
            }
        );
    }

    // BDD: Compile Rewrite and Format
    #[test]
    fn test_compile_rewrite_and_format() {
        // GIVEN: Rewrite(PreviousResult, None), Format(PreviousResult, "JSON")
        let ast = make_ast(vec![
            NormalizedNode::Load {
                resource: "article".to_string(),
                path: "article.txt".to_string(),
            },
            NormalizedNode::Rewrite {
                input: InputRef::PreviousResult,
                prompt: None,
            },
            NormalizedNode::Format {
                input: InputRef::PreviousResult,
                target: "JSON".to_string(),
            },
        ]);
        let ir = compile(&ast).unwrap();
        assert_eq!(
            ir.0[1],
            IrOp::Rewrite {
                input: IrRef(0),
                prompt: None
            }
        );
        assert_eq!(
            ir.0[2],
            IrOp::Format {
                input: IrRef(1),
                target: "JSON".to_string()
            }
        );
    }

    #[test]
    fn test_resource_reference() {
        // Extract from a named resource (not PreviousResult)
        let ast = make_ast(vec![
            NormalizedNode::Load {
                resource: "article".to_string(),
                path: "article.txt".to_string(),
            },
            NormalizedNode::Extract {
                target: "entities".to_string(),
                input: InputRef::Resource("article".to_string()),
            },
        ]);
        let ir = compile(&ast).unwrap();
        assert_eq!(
            ir.0[1],
            IrOp::Extract {
                target: "entities".to_string(),
                input: IrRef(0)
            }
        );
    }

    #[test]
    fn test_empty_program_error() {
        let ast = make_ast(vec![]);
        assert!(compile(&ast).is_err());
    }
}
