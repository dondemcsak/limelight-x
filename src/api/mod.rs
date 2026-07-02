//! `/src/api`: a local, loopback-only HTTP server wrapping the existing
//! `run`/`explain`/`trace` pipeline (spec/api.md). Started via `llx serve`.

pub mod dto;
mod handlers;

use std::sync::Arc;

use axum::routing::post;
use axum::Router;

use crate::error::Error;
use crate::model::ModelAdapter;

/// Shared state for all handlers: the model adapter and the execution lock
/// that serializes pipeline runs (spec/api.md §2 — no parallel execution,
/// per CLAUDE.md §3.3 / architecture.md §6).
pub struct AppState {
    pub adapter: Arc<dyn ModelAdapter + Send + Sync>,
    pub execution_lock: tokio::sync::Mutex<()>,
}

pub type SharedState = Arc<AppState>;

/// Starts the server, binding `127.0.0.1:<port>` and serving `/run`,
/// `/explain`, `/trace` until interrupted (Ctrl+C / SIGINT), per spec/api.md §8.
pub async fn serve(port: u16, adapter: Arc<dyn ModelAdapter + Send + Sync>) -> Result<(), Error> {
    let addr = std::net::SocketAddr::from(([127, 0, 0, 1], port));
    let listener = tokio::net::TcpListener::bind(addr).await?;
    println!("Listening on http://{addr}");
    serve_on(listener, adapter).await
}

/// Serves on an already-bound listener. Used by [`serve`], and by tests that
/// bind port `0` (an OS-assigned ephemeral port) and read the real port back
/// via `listener.local_addr()` before handing the listener off here.
pub async fn serve_on(
    listener: tokio::net::TcpListener,
    adapter: Arc<dyn ModelAdapter + Send + Sync>,
) -> Result<(), Error> {
    let state: SharedState = Arc::new(AppState {
        adapter,
        execution_lock: tokio::sync::Mutex::new(()),
    });

    let app = Router::new()
        .route("/run", post(handlers::run))
        .route("/explain", post(handlers::explain))
        .route("/trace", post(handlers::trace))
        .with_state(state);

    axum::serve(listener, app)
        .with_graceful_shutdown(shutdown_signal())
        .await?;

    Ok(())
}

/// Resolves on Ctrl+C, allowing `axum::serve` to finish any in-flight
/// request before exiting (spec/api.md §8's clean-shutdown requirement).
async fn shutdown_signal() {
    let _ = tokio::signal::ctrl_c().await;
}
