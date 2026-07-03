use std::path::Path;

use crate::error::Error;
use crate::ir::op::{Ir, IrOp, IrRef};
use crate::model::ModelAdapter;
use crate::normalizer::ast::NormalizedAst;
use crate::parser::ast::RawAst;

// ---------------------------------------------------------------------------
// Structured evaluation outcome
// ---------------------------------------------------------------------------

/// The result of a full evaluator run: the final result string plus every
/// prompt sent to, and output received from, the model adapter along the way.
///
/// `prompts`/`model_outputs` are always collected regardless of `trace`, so
/// callers that need structured data (e.g. `/src/api`'s `/trace` endpoint)
/// don't have to re-run the pipeline or scrape stdout.
#[derive(Debug, Clone)]
pub struct EvalOutcome {
    pub final_result: String,
    pub prompts: Vec<PromptRecord>,
    pub model_outputs: Vec<ModelOutputRecord>,
}

#[derive(Debug, Clone)]
pub struct PromptRecord {
    pub operation_index: usize,
    pub prompt_text: String,
}

#[derive(Debug, Clone)]
pub struct ModelOutputRecord {
    pub operation_index: usize,
    pub raw_text: String,
    pub latency_ms: u128,
}

// ---------------------------------------------------------------------------
// Public entry points
// ---------------------------------------------------------------------------

/// Evaluate an IR program and return the final result plus structured trace data.
pub fn evaluate(
    ir: &Ir,
    adapter: &dyn ModelAdapter,
    base_dir: &Path,
    trace: bool,
    raw_ast: Option<&RawAst>,
    normalized_ast: Option<&NormalizedAst>,
) -> Result<EvalOutcome, Error> {
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
    let mut prompts: Vec<PromptRecord> = Vec::new();
    let mut model_outputs: Vec<ModelOutputRecord> = Vec::new();

    for (index, op) in ir.0.iter().enumerate() {
        let op_name = op_name(op);

        if trace {
            println!("\n--- Operation [{index}]: {op} ---");
        }

        let result = execute_op(
            op,
            &results,
            adapter,
            base_dir,
            index,
            op_name,
            trace,
            &mut prompts,
            &mut model_outputs,
        )?;
        results.push(result);
    }

    let final_result = results.into_iter().last().ok_or_else(|| Error::EvalError {
        index: 0,
        op: "unknown".to_string(),
        message: "no operations to evaluate".to_string(),
    })?;

    Ok(EvalOutcome {
        final_result,
        prompts,
        model_outputs,
    })
}

// ---------------------------------------------------------------------------
// Per-operation execution
// ---------------------------------------------------------------------------

#[allow(clippy::too_many_arguments)]
fn execute_op(
    op: &IrOp,
    results: &[String],
    adapter: &dyn ModelAdapter,
    base_dir: &Path,
    index: usize,
    op_name: &str,
    trace: bool,
    prompts: &mut Vec<PromptRecord>,
    model_outputs: &mut Vec<ModelOutputRecord>,
) -> Result<String, Error> {
    match op {
        IrOp::Load { path } => {
            let resolved = {
                let p = Path::new(path);
                if p.is_absolute() {
                    p.to_path_buf()
                } else {
                    base_dir.join(p)
                }
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
            call_model(
                adapter,
                &prompt,
                index,
                op_name,
                trace,
                prompts,
                model_outputs,
            )
        }

        IrOp::Summarize { input, prompt } => {
            let text = resolve_ref(input, results, index, op_name)?;
            let full_prompt = match prompt {
                Some(user_prompt) => format!("{user_prompt}\n\nInput:\n{text}"),
                None => format!("Summarize the following text clearly and concisely:\n\n{text}"),
            };
            call_model(
                adapter,
                &full_prompt,
                index,
                op_name,
                trace,
                prompts,
                model_outputs,
            )
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
            call_model(
                adapter,
                &full_prompt,
                index,
                op_name,
                trace,
                prompts,
                model_outputs,
            )
        }

        IrOp::Rewrite { input, prompt } => {
            let text = resolve_ref(input, results, index, op_name)?;
            let full_prompt = match prompt {
                Some(user_prompt) => format!("{user_prompt}\n\nInput:\n{text}"),
                None => {
                    format!("Rewrite the following text for clarity and readability:\n\n{text}")
                }
            };
            call_model(
                adapter,
                &full_prompt,
                index,
                op_name,
                trace,
                prompts,
                model_outputs,
            )
        }

        IrOp::Format { input, target } => {
            let text = resolve_ref(input, results, index, op_name)?;
            if target.eq_ignore_ascii_case("JSON") {
                let prompt = format!(
                    "Convert the following text into a pipe-delimited Markdown table with a header row:\n\n{text}"
                );
                let table = call_model(
                    adapter,
                    &prompt,
                    index,
                    op_name,
                    trace,
                    prompts,
                    model_outputs,
                )?;
                table_to_json(&table, index, op_name)
            } else {
                let prompt = format!("Format the following text as {target}:\n\n{text}");
                call_model(
                    adapter,
                    &prompt,
                    index,
                    op_name,
                    trace,
                    prompts,
                    model_outputs,
                )
            }
        }
    }
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

fn table_to_json(table: &str, index: usize, op_name: &str) -> Result<String, Error> {
    let make_err = |msg: &str| Error::EvalError {
        index,
        op: op_name.to_string(),
        message: msg.to_string(),
    };

    let is_separator = |line: &str| {
        line.chars()
            .all(|c| c == '|' || c == '-' || c == ' ' || c == ':')
    };

    let data_rows: Vec<Vec<String>> = table
        .lines()
        .filter(|l| !l.trim().is_empty() && !is_separator(l.trim()))
        .map(|l| {
            l.split('|')
                .map(|cell| strip_markdown(cell.trim()))
                .filter(|cell| !cell.is_empty())
                .collect()
        })
        .collect();

    if data_rows.is_empty() {
        return Err(make_err(
            "model output is not a valid pipe-delimited table: no rows found",
        ));
    }

    let headers = &data_rows[0];
    if headers.is_empty() {
        return Err(make_err(
            "model output is not a valid pipe-delimited table: no header columns found",
        ));
    }

    let mut json = String::from("[");
    for (i, row) in data_rows[1..].iter().enumerate() {
        if i > 0 {
            json.push(',');
        }
        json.push('{');
        for (j, header) in headers.iter().enumerate() {
            if j > 0 {
                json.push(',');
            }
            let value = row.get(j).map(String::as_str).unwrap_or("");
            json.push('"');
            json.push_str(&escape_json_str(header));
            json.push_str("\":\"");
            json.push_str(&escape_json_str(value));
            json.push('"');
        }
        json.push('}');
    }
    json.push(']');
    Ok(json)
}

fn strip_markdown(s: &str) -> String {
    s.replace("**", "")
        .replace("__", "")
        .replace(['*', '_'], "")
}

fn escape_json_str(s: &str) -> String {
    s.replace('\\', "\\\\").replace('"', "\\\"")
}

fn resolve_ref<'a>(
    ir_ref: &IrRef,
    results: &'a [String],
    index: usize,
    op_name: &str,
) -> Result<&'a str, Error> {
    results
        .get(ir_ref.0)
        .map(String::as_str)
        .ok_or_else(|| Error::EvalError {
            index,
            op: op_name.to_string(),
            message: format!("undefined IR reference ${}", ir_ref.0),
        })
}

#[allow(clippy::too_many_arguments)]
fn call_model(
    adapter: &dyn ModelAdapter,
    prompt: &str,
    index: usize,
    op_name: &str,
    trace: bool,
    prompts: &mut Vec<PromptRecord>,
    model_outputs: &mut Vec<ModelOutputRecord>,
) -> Result<String, Error> {
    if trace {
        println!("Prompt:\n{prompt}\n");
    }
    prompts.push(PromptRecord {
        operation_index: index,
        prompt_text: prompt.to_string(),
    });
    let started = std::time::Instant::now();
    let output = adapter.complete(prompt).map_err(|e| Error::EvalError {
        index,
        op: op_name.to_string(),
        message: e.to_string(),
    })?;
    let latency_ms = started.elapsed().as_millis();
    if trace {
        println!("Model output:\n{output}");
    }
    model_outputs.push(ModelOutputRecord {
        operation_index: index,
        raw_text: output.clone(),
        latency_ms,
    });
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
            .map(|outcome| outcome.final_result)
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
            IrOp::Load {
                path: make_temp_file("article text"),
            },
            IrOp::Summarize {
                input: IrRef(0),
                prompt: None,
            },
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
            IrOp::Load {
                path: make_temp_file("article text"),
            },
            IrOp::Summarize {
                input: IrRef(0),
                prompt: Some("Summarize in 3 bullets.".to_string()),
            },
        ]);
        eval(ir, &adapter).unwrap();
        let prompt = adapter.last_prompt().unwrap();
        assert!(
            prompt.starts_with("Summarize in 3 bullets."),
            "unexpected prompt: {prompt}"
        );
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
            IrOp::Load {
                path: make_temp_file("some text"),
            },
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
            IrOp::Load {
                path: make_temp_file("draft text"),
            },
            IrOp::Rewrite {
                input: IrRef(0),
                prompt: None,
            },
        ]);
        eval(ir, &adapter).unwrap();
        let prompt = adapter.last_prompt().unwrap();
        assert!(
            prompt.contains("Rewrite the following text for clarity and readability:"),
            "unexpected prompt: {prompt}"
        );
    }

    // BDD: Evaluate Format as JSON — model receives tabular prompt, evaluator converts to JSON
    #[test]
    fn test_evaluate_format_json_tabular_prompt_and_conversion() {
        // GIVEN: Format { input: "$0", target: "JSON" }
        // WHEN the evaluator runs
        // THEN the model receives a prompt asking for a pipe-delimited table,
        //      and the result is a JSON array produced by the evaluator (not the model)
        let table_output = "| **Name** | **Age** |\n|------|-----|\n| Alice | 30 |\n| Bob | 25 |";
        let adapter = CapturingAdapter::new(table_output);
        let ir = Ir(vec![
            IrOp::Load {
                path: make_temp_file("some data"),
            },
            IrOp::Format {
                input: IrRef(0),
                target: "JSON".to_string(),
            },
        ]);
        let result = eval(ir, &adapter).unwrap();
        let prompt = adapter.last_prompt().unwrap();
        assert!(
            prompt.contains("pipe-delimited Markdown table with a header row"),
            "unexpected prompt: {prompt}"
        );
        assert_eq!(
            result,
            r#"[{"Name":"Alice","Age":"30"},{"Name":"Bob","Age":"25"}]"#
        );
    }

    // BDD: Evaluate Format as non-JSON target — model receives standard format prompt
    #[test]
    fn test_evaluate_format_non_json_delegates_to_model() {
        // GIVEN: Format { input: "$0", target: "Markdown" }
        // WHEN the evaluator runs
        // THEN the model receives the built-in format template
        let adapter = CapturingAdapter::new("## Markdown output");
        let ir = Ir(vec![
            IrOp::Load {
                path: make_temp_file("some data"),
            },
            IrOp::Format {
                input: IrRef(0),
                target: "Markdown".to_string(),
            },
        ]);
        eval(ir, &adapter).unwrap();
        let prompt = adapter.last_prompt().unwrap();
        assert!(
            prompt.contains("Format the following text as Markdown:"),
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

    // EvalOutcome carries structured prompt/model-output records regardless of trace
    #[test]
    fn test_eval_outcome_collects_prompts_and_model_outputs() {
        let adapter = CapturingAdapter::new("summary text");
        let ir = Ir(vec![
            IrOp::Load {
                path: make_temp_file("article text"),
            },
            IrOp::Summarize {
                input: IrRef(0),
                prompt: None,
            },
        ]);
        let outcome =
            evaluate(&ir, &adapter, std::path::Path::new("."), false, None, None).unwrap();
        assert_eq!(outcome.final_result, "summary text");
        assert_eq!(outcome.prompts.len(), 1);
        assert_eq!(outcome.prompts[0].operation_index, 1);
        assert!(outcome.prompts[0].prompt_text.contains("article text"));
        assert_eq!(outcome.model_outputs.len(), 1);
        assert_eq!(outcome.model_outputs[0].operation_index, 1);
        assert_eq!(outcome.model_outputs[0].raw_text, "summary text");
    }

    // BDD: Trace mode shows AST, normalized AST, IR, prompts, and results
    #[test]
    fn test_trace_mode_does_not_panic() {
        // GIVEN a valid IR with trace=true
        // WHEN the evaluator runs
        // THEN it executes without error (output goes to stdout)
        let adapter = MockModelAdapter::new("summary");
        let ir = Ir(vec![
            IrOp::Load {
                path: make_temp_file("some text"),
            },
            IrOp::Summarize {
                input: IrRef(0),
                prompt: None,
            },
        ]);
        let result = evaluate(&ir, &adapter, std::path::Path::new("."), true, None, None);
        assert!(result.is_ok());
    }

    fn make_temp_file(content: &str) -> String {
        use std::env;
        static COUNTER: std::sync::atomic::AtomicU64 = std::sync::atomic::AtomicU64::new(0);
        let id = COUNTER.fetch_add(1, std::sync::atomic::Ordering::SeqCst);
        let path = env::temp_dir().join(format!("llx_test_{id}.txt"));
        std::fs::write(&path, content).unwrap();
        path.to_str().unwrap().to_string()
    }
}
