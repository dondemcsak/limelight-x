use std::path::Path;

use crate::error::Error;
use crate::ir::op::{Ir, IrOp, IrRef};
use crate::model::ModelAdapter;
use crate::normalizer::ast::NormalizedAst;
use crate::parser::ast::RawAst;

// ---------------------------------------------------------------------------
// Public entry points
// ---------------------------------------------------------------------------

/// Evaluate an IR program and return the final result string.
pub fn evaluate(
    ir: &Ir,
    adapter: &dyn ModelAdapter,
    base_dir: &Path,
    trace: bool,
    raw_ast: Option<&RawAst>,
    normalized_ast: Option<&NormalizedAst>,
) -> Result<String, Error> {
    if trace {
        if let Some(raw) = raw_ast {
            println!("=== Raw AST ===");
            println!("{raw:#?}");
        }
        if let Some(norm) = normalized_ast {
            println!("\n=== Normalized AST ===");
            println!("{norm:#?}");
        }
        println!("\n=== IR ===");
        println!("{ir}");
        println!("\n=== Evaluation ===");
    }

    let mut results: Vec<String> = Vec::new();

    for (index, op) in ir.0.iter().enumerate() {
        let op_name = op_name(op);

        if trace {
            println!("\n--- Operation [{index}]: {op} ---");
        }

        let result = execute_op(op, &results, adapter, base_dir, index, op_name, trace)?;
        results.push(result);
    }

    results
        .into_iter()
        .last()
        .ok_or_else(|| Error::EvalError {
            index: 0,
            op: "unknown".to_string(),
            message: "no operations to evaluate".to_string(),
        })
}

// ---------------------------------------------------------------------------
// Per-operation execution
// ---------------------------------------------------------------------------

fn execute_op(
    op: &IrOp,
    results: &[String],
    adapter: &dyn ModelAdapter,
    base_dir: &Path,
    index: usize,
    op_name: &str,
    trace: bool,
) -> Result<String, Error> {
    match op {
        IrOp::Load { path } => {
            let resolved = {
                let p = Path::new(path);
                if p.is_absolute() { p.to_path_buf() } else { base_dir.join(p) }
            };
            let content = std::fs::read_to_string(&resolved).map_err(|e| Error::EvalError {
                index,
                op: op_name.to_string(),
                message: format!("failed to read file '{path}': {e}"),
            })?;
            if trace {
                println!("Loaded {len} bytes from '{path}'", len = content.len());
            }
            Ok(content)
        }

        IrOp::Extract { target, input } => {
            let text = resolve_ref(input, results, index, op_name)?;
            let prompt = format!("Extract the {target} from the following text:\n\n{text}");
            call_model(adapter, &prompt, index, op_name, trace)
        }

        IrOp::Summarize { input, prompt } => {
            let text = resolve_ref(input, results, index, op_name)?;
            let full_prompt = match prompt {
                Some(user_prompt) => format!("{user_prompt}\n\nInput:\n{text}"),
                None => format!("Summarize the following text clearly and concisely:\n\n{text}"),
            };
            call_model(adapter, &full_prompt, index, op_name, trace)
        }

        IrOp::Translate {
            input,
            language,
            prompt,
        } => {
            let text = resolve_ref(input, results, index, op_name)?;
            let full_prompt = match prompt {
                Some(user_prompt) => format!("{user_prompt}\n\nInput:\n{text}"),
                None => format!("Translate the following text into {language}:\n\n{text}"),
            };
            call_model(adapter, &full_prompt, index, op_name, trace)
        }

        IrOp::Rewrite { input, prompt } => {
            let text = resolve_ref(input, results, index, op_name)?;
            let full_prompt = match prompt {
                Some(user_prompt) => format!("{user_prompt}\n\nInput:\n{text}"),
                None => {
                    format!("Rewrite the following text for clarity and readability:\n\n{text}")
                }
            };
            call_model(adapter, &full_prompt, index, op_name, trace)
        }

        IrOp::Format { input, target } => {
            let text = resolve_ref(input, results, index, op_name)?;
            let prompt = format!("Format the following text as {target}:\n\n{text}");
            call_model(adapter, &prompt, index, op_name, trace)
        }
    }
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

fn resolve_ref<'a>(
    ir_ref: &IrRef,
    results: &'a [String],
    index: usize,
    op_name: &str,
) -> Result<&'a str, Error> {
    results.get(ir_ref.0).map(String::as_str).ok_or_else(|| Error::EvalError {
        index,
        op: op_name.to_string(),
        message: format!("undefined IR reference ${}", ir_ref.0),
    })
}

fn call_model(
    adapter: &dyn ModelAdapter,
    prompt: &str,
    index: usize,
    op_name: &str,
    trace: bool,
) -> Result<String, Error> {
    if trace {
        println!("Prompt:\n{prompt}\n");
    }
    let output = adapter.complete(prompt).map_err(|e| Error::EvalError {
        index,
        op: op_name.to_string(),
        message: e.to_string(),
    })?;
    if trace {
        println!("Model output:\n{output}");
    }
    Ok(output)
}

fn op_name(op: &IrOp) -> &'static str {
    match op {
        IrOp::Load { .. } => "Load",
        IrOp::Extract { .. } => "Extract",
        IrOp::Summarize { .. } => "Summarize",
        IrOp::Translate { .. } => "Translate",
        IrOp::Rewrite { .. } => "Rewrite",
        IrOp::Format { .. } => "Format",
    }
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

#[cfg(test)]
mod tests {
    use super::*;
    use crate::ir::op::{Ir, IrOp, IrRef};
    use crate::model::mock::{CapturingAdapter, MockModelAdapter};

    fn eval(ir: Ir, adapter: &dyn ModelAdapter) -> Result<String, Error> {
        evaluate(&ir, adapter, std::path::Path::new("."), false, None, None)
    }

    // BDD: Evaluate a Load operation
    #[test]
    fn test_evaluate_load() {
        // GIVEN: Load { path: "<temp file>" }
        // WHEN the evaluator runs
        // THEN results[0] contains the file contents
        let path = make_temp_file("hello world");
        let ir = Ir(vec![IrOp::Load { path }]);
        let adapter = MockModelAdapter::new("unused");
        let result = eval(ir, &adapter).unwrap();
        assert_eq!(result, "hello world");
    }

    // BDD: Evaluate Summarize with built-in prompt
    #[test]
    fn test_evaluate_summarize_builtin_prompt() {
        // GIVEN: Summarize { input: "$0", prompt: None }
        // WHEN the evaluator runs
        // THEN the adapter receives the built-in template
        let adapter = CapturingAdapter::new("summary text");
        let ir = Ir(vec![
            IrOp::Load { path: make_temp_file("article text") },
            IrOp::Summarize { input: IrRef(0), prompt: None },
        ]);
        eval(ir, &adapter).unwrap();
        let prompt = adapter.last_prompt().unwrap();
        assert!(
            prompt.starts_with("Summarize the following text clearly and concisely:"),
            "unexpected prompt: {prompt}"
        );
        assert!(prompt.contains("article text"));
    }

    // BDD: Evaluate Summarize with expression hole
    #[test]
    fn test_evaluate_summarize_custom_prompt() {
        // GIVEN: Summarize { input: "$0", prompt: Some("Summarize in 3 bullets.") }
        // WHEN the evaluator runs
        // THEN the adapter receives the user prompt with input appended
        let adapter = CapturingAdapter::new("bullet summary");
        let ir = Ir(vec![
            IrOp::Load { path: make_temp_file("article text") },
            IrOp::Summarize {
                input: IrRef(0),
                prompt: Some("Summarize in 3 bullets.".to_string()),
            },
        ]);
        eval(ir, &adapter).unwrap();
        let prompt = adapter.last_prompt().unwrap();
        assert!(prompt.starts_with("Summarize in 3 bullets."), "unexpected prompt: {prompt}");
        assert!(prompt.contains("Input:"));
        assert!(prompt.contains("article text"));
    }

    // BDD: Evaluate Translate
    #[test]
    fn test_evaluate_translate_builtin_prompt() {
        // GIVEN: Translate { input: "$0", language: "French", prompt: None }
        // WHEN the evaluator runs
        // THEN the adapter receives the built-in translation template
        let adapter = CapturingAdapter::new("traduction");
        let ir = Ir(vec![
            IrOp::Load { path: make_temp_file("some text") },
            IrOp::Translate {
                input: IrRef(0),
                language: "French".to_string(),
                prompt: None,
            },
        ]);
        eval(ir, &adapter).unwrap();
        let prompt = adapter.last_prompt().unwrap();
        assert!(
            prompt.contains("Translate the following text into French:"),
            "unexpected prompt: {prompt}"
        );
    }

    // BDD: Evaluate Rewrite
    #[test]
    fn test_evaluate_rewrite_builtin_prompt() {
        // GIVEN: Rewrite { input: "$0", prompt: None }
        // WHEN the evaluator runs
        // THEN the adapter receives the built-in rewrite template
        let adapter = CapturingAdapter::new("rewritten");
        let ir = Ir(vec![
            IrOp::Load { path: make_temp_file("draft text") },
            IrOp::Rewrite { input: IrRef(0), prompt: None },
        ]);
        eval(ir, &adapter).unwrap();
        let prompt = adapter.last_prompt().unwrap();
        assert!(
            prompt.contains("Rewrite the following text for clarity and readability:"),
            "unexpected prompt: {prompt}"
        );
    }

    // BDD: Evaluate Format
    #[test]
    fn test_evaluate_format_builtin_prompt() {
        // GIVEN: Format { input: "$0", target: "JSON" }
        // WHEN the evaluator runs
        // THEN the adapter receives the built-in format template
        let adapter = CapturingAdapter::new("{\"key\": \"value\"}");
        let ir = Ir(vec![
            IrOp::Load { path: make_temp_file("some data") },
            IrOp::Format { input: IrRef(0), target: "JSON".to_string() },
        ]);
        eval(ir, &adapter).unwrap();
        let prompt = adapter.last_prompt().unwrap();
        assert!(
            prompt.contains("Format the following text as JSON:"),
            "unexpected prompt: {prompt}"
        );
    }

    // BDD: Missing file error
    #[test]
    fn test_missing_file_error() {
        // GIVEN: Load { path: "missing.txt" }
        // WHEN the evaluator runs
        // THEN a fatal EvalError is returned with the operation index
        let ir = Ir(vec![IrOp::Load {
            path: "nonexistent_file_abc123.txt".to_string(),
        }]);
        let adapter = MockModelAdapter::new("unused");
        let err = eval(ir, &adapter).unwrap_err();
        let msg = err.to_string();
        assert!(msg.contains("operation 0"), "unexpected error: {msg}");
    }

    // BDD: Trace mode shows AST, normalized AST, IR, prompts, and results
    #[test]
    fn test_trace_mode_does_not_panic() {
        // GIVEN a valid IR with trace=true
        // WHEN the evaluator runs
        // THEN it executes without error (output goes to stdout)
        let adapter = MockModelAdapter::new("summary");
        let ir = Ir(vec![
            IrOp::Load { path: make_temp_file("some text") },
            IrOp::Summarize { input: IrRef(0), prompt: None },
        ]);
        let result = evaluate(&ir, &adapter, std::path::Path::new("."), true, None, None);
        assert!(result.is_ok());
    }

    fn make_temp_file(content: &str) -> String {
        use std::env;
        static COUNTER: std::sync::atomic::AtomicU64 =
            std::sync::atomic::AtomicU64::new(0);
        let id = COUNTER.fetch_add(1, std::sync::atomic::Ordering::SeqCst);
        let path = env::temp_dir().join(format!("llx_test_{id}.txt"));
        std::fs::write(&path, content).unwrap();
        path.to_str().unwrap().to_string()
    }
}
