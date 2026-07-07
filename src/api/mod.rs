//! `/src/api`: a local, loopback-only HTTP + WebSocket server wrapping the
//! existing `run`/`explain`/`trace` pipeline (spec/api.md). Started via
//! `llx serve`. Pipeline results are streamed incrementally as JSON events
//! over `/events` rather than returned synchronously from the POST handlers.

pub mod dto;
mod handlers;
mod worker;
mod ws;

use std::sync::atomic::AtomicU64;
use std::sync::Arc;

use axum::routing::{get, post};
use axum::Router;
use tokio::sync::mpsc;

use crate::error::Error;
use crate::model::ModelAdapter;

/// Shared state for all handlers: the model adapter, the job queue that
/// serializes pipeline execution (see `worker.rs`), the current WebSocket
/// client's outbound sender, and the `correlation_id` counter.
pub struct AppState {
    pub adapter: Arc<dyn ModelAdapter + Send + Sync>,
    pub job_tx: mpsc::UnboundedSender<worker::PipelineJob>,
    pub client_tx: ws::ClientSender,
    pub next_correlation_id: AtomicU64,
}

pub type SharedState = Arc<AppState>;

/// Starts the server, binding `127.0.0.1:<port>` and serving `/run`,
/// `/explain`, `/trace`, `/events` until interrupted (Ctrl+C / SIGINT), per
/// spec/api.md §8.
pub async fn serve(port: u16, adapter: Arc<dyn ModelAdapter + Send + Sync>) -> Result<(), Error> {
    let addr = std::net::SocketAddr::from(([127, 0, 0, 1], port));
    let listener = tokio::net::TcpListener::bind(addr).await.map_err(|e| {
        Error::IoError(std::io::Error::new(
            e.kind(),
            format!("failed to bind {addr}: {e}"),
        ))
    })?;
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
    let (job_tx, job_rx) = mpsc::unbounded_channel();
    let state: SharedState = Arc::new(AppState {
        adapter,
        job_tx,
        client_tx: ws::ClientSender::default(),
        next_correlation_id: AtomicU64::new(0),
    });

    tokio::spawn(worker::run_worker(job_rx, Arc::clone(&state)));

    let app = Router::new()
        .route("/run", post(handlers::run))
        .route("/explain", post(handlers::explain))
        .route("/trace", post(handlers::trace))
        .route("/events", get(ws::ws_handler))
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
