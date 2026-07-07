use clap::{Parser, Subcommand};
use std::path::PathBuf;
use std::sync::Arc;

use limelight_x::error::Error;
use limelight_x::model::claude::ClaudeModelAdapter;
use limelight_x::model::ModelAdapter;
use limelight_x::{api, evaluator, ir, normalizer, parser};

// ---------------------------------------------------------------------------
// CLI definition
// ---------------------------------------------------------------------------

/// Limelight-X: a minimal CNL expression layer.
#[derive(Parser)]
#[command(name = "llx", version = "0.5.1")]
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
    /// Start the local HTTP API server (see spec/api.md).
    Serve {
        /// Port to bind on 127.0.0.1 (spec default: 4747).
        #[arg(long, default_value_t = 4747)]
        port: u16,
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

/// `Run`/`Explain`/`Trace` stay fully synchronous, exactly as before —
/// only `Serve` needs an async runtime, so only it builds one. Building a
/// runtime unconditionally (e.g. via `#[tokio::main]`) would put the other
/// three commands inside an async context too, and `ClaudeModelAdapter`
/// uses `reqwest::blocking`, which panics if constructed from within one
/// (see `cmd_serve`'s spawn_blocking below for the same reason).
fn run(cli: Cli) -> Result<(), Error> {
    match cli.command {
        Command::Run { file } => cmd_run(&file),
        Command::Explain { file } => cmd_explain(&file),
        Command::Trace { file } => cmd_trace(&file),
        Command::Serve { port } => {
            let rt = tokio::runtime::Builder::new_multi_thread()
                .enable_all()
                .build()
                .expect("failed to start async runtime");
            rt.block_on(cmd_serve(port))
        }
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
    let outcome = evaluator::evaluate(&program, &adapter, base_dir, false, None, None, None)?;
    println!("{}", outcome.final_result);
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
    let outcome = evaluator::evaluate(
        &program,
        &adapter,
        base_dir,
        true,
        Some(&raw_ast),
        Some(&normalized_ast),
        None,
    )?;
    println!("\n=== Final Result ===");
    println!("{}", outcome.final_result);
    Ok(())
}

// ---------------------------------------------------------------------------
// llx serve [--port <N>]
// ---------------------------------------------------------------------------

async fn cmd_serve(port: u16) -> Result<(), Error> {
    // Fail fast if ANTHROPIC_API_KEY is unset, before binding (spec/api.md §8).
    // Constructing `ClaudeModelAdapter` builds a `reqwest::blocking::Client`,
    // which panics if built directly on a Tokio runtime thread — run it via
    // spawn_blocking, the same pattern every request handler uses.
    let adapter = tokio::task::spawn_blocking(ClaudeModelAdapter::new)
        .await
        .expect("adapter construction task panicked")?;
    let adapter: Arc<dyn ModelAdapter + Send + Sync> = Arc::new(adapter);
    api::serve(port, adapter).await
}
