use clap::{Parser, Subcommand};
use std::path::PathBuf;

use limelight_x::error::Error;
use limelight_x::model::claude::ClaudeModelAdapter;
use limelight_x::{evaluator, ir, normalizer, parser};

// ---------------------------------------------------------------------------
// CLI definition
// ---------------------------------------------------------------------------

/// Limelight-X: a minimal CNL expression layer.
#[derive(Parser)]
#[command(name = "llx", version = "0.1.0")]
struct Cli {
    #[command(subcommand)]
    command: Command,
}

#[derive(Subcommand)]
enum Command {
    /// Execute a .llx program and print the final result.
    Run {
        /// Path to the .llx file.
        file: PathBuf,
    },
    /// Show the raw AST, normalized AST, and IR without evaluating.
    Explain {
        /// Path to the .llx file.
        file: PathBuf,
    },
    /// Execute with full trace output (AST → IR → prompts → model output → result).
    Trace {
        /// Path to the .llx file.
        file: PathBuf,
    },
}

// ---------------------------------------------------------------------------
// Entry point
// ---------------------------------------------------------------------------

fn main() {
    let cli = Cli::parse();
    if let Err(e) = run(cli) {
        eprintln!("error: {e}");
        std::process::exit(1);
    }
}

fn run(cli: Cli) -> Result<(), Error> {
    match cli.command {
        Command::Run { file } => cmd_run(&file),
        Command::Explain { file } => cmd_explain(&file),
        Command::Trace { file } => cmd_trace(&file),
    }
}

// ---------------------------------------------------------------------------
// llx run <file>
// ---------------------------------------------------------------------------

fn cmd_run(file: &PathBuf) -> Result<(), Error> {
    let base_dir = file.parent().unwrap_or(std::path::Path::new("."));
    let source = std::fs::read_to_string(file)?;
    let raw_ast = parser::parse(&source)?;
    let normalized_ast = normalizer::normalize(&raw_ast)?;
    let program = ir::compiler::compile(&normalized_ast)?;
    let adapter = ClaudeModelAdapter::new()?;
    let result = evaluator::evaluate(&program, &adapter, base_dir, false, None, None)?;
    println!("{result}");
    Ok(())
}

// ---------------------------------------------------------------------------
// llx explain <file>
// ---------------------------------------------------------------------------

fn cmd_explain(file: &PathBuf) -> Result<(), Error> {
    let source = std::fs::read_to_string(file)?;
    let raw_ast = parser::parse(&source)?;
    let normalized_ast = normalizer::normalize(&raw_ast)?;
    let program = ir::compiler::compile(&normalized_ast)?;

    println!("=== Raw AST ===");
    println!("{raw_ast:#?}");

    println!("\n=== Normalized AST ===");
    println!("{normalized_ast:#?}");

    println!("\n=== IR ===");
    println!("{program}");

    Ok(())
}

// ---------------------------------------------------------------------------
// llx trace <file>
// ---------------------------------------------------------------------------

fn cmd_trace(file: &PathBuf) -> Result<(), Error> {
    let base_dir = file.parent().unwrap_or(std::path::Path::new("."));
    let source = std::fs::read_to_string(file)?;
    let raw_ast = parser::parse(&source)?;
    let normalized_ast = normalizer::normalize(&raw_ast)?;
    let program = ir::compiler::compile(&normalized_ast)?;
    let adapter = ClaudeModelAdapter::new()?;
    let result = evaluator::evaluate(
        &program, &adapter, base_dir, true, Some(&raw_ast), Some(&normalized_ast),
    )?;
    println!("\n=== Final Result ===");
    println!("{result}");
    Ok(())
}
