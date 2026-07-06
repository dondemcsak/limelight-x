//! WebSocket transport for streamed pipeline events (spec/api.md §2.3).
//!
//! Exactly one UI client is expected (a local desktop app) — no fan-out to
//! multiple subscribers is implemented. A new connection replaces whatever
//! sender is currently stored ("last connection wins"), which is also the
//! reconnect story after a Settings-triggered `llx serve` relaunch.

use std::sync::Mutex;

use axum::extract::ws::{Message, WebSocket, WebSocketUpgrade};
use axum::extract::State;
use axum::response::Response;
use tokio::sync::mpsc;

use super::dto::Event;
use super::SharedState;

/// Holds the outbound channel to the currently-connected client, if any.
#[derive(Default)]
pub struct ClientSender(Mutex<Option<mpsc::UnboundedSender<Message>>>);

impl ClientSender {
    /// Sends `event` to the currently-connected client. Silently drops the
    /// event if no client is connected — pipeline execution is never gated
    /// on WS presence, and spec/api.md §2.3's ordering/determinism
    /// guarantees are about emission order, not delivery guarantees.
    pub fn send(&self, event: Event) {
        let sender = self.0.lock().expect("client sender mutex poisoned");
        if let Some(tx) = sender.as_ref() {
            let text = serde_json::to_string(&event).expect("Event must be serializable");
            // Ignore send errors: a full/closed channel means the read loop
            // will observe the disconnect and clear this sender shortly.
            let _ = tx.send(Message::Text(text));
        }
    }

    fn set(&self, tx: mpsc::UnboundedSender<Message>) {
        *self.0.lock().expect("client sender mutex poisoned") = Some(tx);
    }

    /// Clears the stored sender, but only if it's still `tx` — an old
    /// connection's teardown must not clobber a newer one that already
    /// replaced it.
    fn clear_if_current(&self, tx: &mpsc::UnboundedSender<Message>) {
        let mut guard = self.0.lock().expect("client sender mutex poisoned");
        if guard
            .as_ref()
            .is_some_and(|current| current.same_channel(tx))
        {
            *guard = None;
        }
    }
}

pub async fn ws_handler(ws: WebSocketUpgrade, State(state): State<SharedState>) -> Response {
    ws.on_upgrade(move |socket| handle_socket(socket, state))
}

async fn handle_socket(mut socket: WebSocket, state: SharedState) {
    let (tx, mut rx) = mpsc::unbounded_channel::<Message>();
    state.client_tx.set(tx.clone());

    loop {
        tokio::select! {
            outgoing = rx.recv() => {
                match outgoing {
                    Some(msg) => {
                        if socket.send(msg).await.is_err() {
                            break;
                        }
                    }
                    None => break,
                }
            }
            incoming = socket.recv() => {
                match incoming {
                    Some(Ok(_)) => continue, // inbound frames are ignored
                    _ => break,              // close frame, error, or stream end
                }
            }
        }
    }

    state.client_tx.clear_if_current(&tx);
}
