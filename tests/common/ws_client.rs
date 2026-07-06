//! A minimal RFC 6455 WebSocket client for integration tests.
//!
//! No WebSocket client crate is added as a (dev-)dependency — `/src/api`'s
//! only new dependency for streaming is axum's `ws` feature. This client is
//! sufficient for tests: it performs the HTTP Upgrade handshake, then reads
//! unmasked, non-fragmented server->client text frames (which is all
//! `/events` ever sends).

use tokio::io::{AsyncReadExt, AsyncWriteExt};
use tokio::net::TcpStream;

pub struct TestWsClient {
    stream: TcpStream,
}

impl TestWsClient {
    /// Connects to `{base_url}/events` and performs the WebSocket handshake.
    /// `base_url` must be an `http://host:port` string (as returned by
    /// `spawn_test_server`).
    pub async fn connect(base_url: &str) -> Self {
        let host_port = base_url
            .strip_prefix("http://")
            .expect("base_url must be an http:// URL");
        let mut stream = TcpStream::connect(host_port)
            .await
            .expect("failed to connect to test server");

        // A fixed key is fine here — RFC 6455 requires the server to derive
        // `Sec-WebSocket-Accept` from it, not that it be unpredictable; this
        // is the example key from RFC 6455 §1.2.
        let request = format!(
            "GET /events HTTP/1.1\r\n\
             Host: {host_port}\r\n\
             Upgrade: websocket\r\n\
             Connection: Upgrade\r\n\
             Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\n\
             Sec-WebSocket-Version: 13\r\n\
             \r\n"
        );
        stream
            .write_all(request.as_bytes())
            .await
            .expect("failed to send WS handshake request");

        let mut buf = Vec::new();
        loop {
            let mut byte = [0u8; 1];
            stream
                .read_exact(&mut byte)
                .await
                .expect("connection closed during WS handshake");
            buf.push(byte[0]);
            if buf.ends_with(b"\r\n\r\n") {
                break;
            }
        }
        let response = String::from_utf8_lossy(&buf);
        assert!(
            response.starts_with("HTTP/1.1 101"),
            "expected a 101 Switching Protocols response, got: {response}"
        );

        // Give the server's spawned connection-handling task a chance to
        // register this connection as the current client sender before the
        // caller triggers a pipeline run — `ws.on_upgrade` spawns that task
        // after the 101 response is already on the wire, so without this
        // there's a narrow window where an immediately-following POST could
        // have its `pipeline_started` event dropped.
        for _ in 0..32 {
            tokio::task::yield_now().await;
        }

        Self { stream }
    }

    /// Reads one WebSocket text frame and parses it as JSON. Panics on a
    /// close frame or connection error — tests should read exactly as many
    /// events as the scenario is expected to produce.
    pub async fn recv_json(&mut self) -> serde_json::Value {
        let text = self.recv_text().await;
        serde_json::from_str(&text).expect("event frame must be valid JSON")
    }

    async fn recv_text(&mut self) -> String {
        loop {
            let mut header = [0u8; 2];
            self.stream
                .read_exact(&mut header)
                .await
                .expect("connection closed while waiting for a WS frame");
            let opcode = header[0] & 0x0F;
            let masked = header[1] & 0x80 != 0;
            let mut len = u64::from(header[1] & 0x7F);

            if len == 126 {
                let mut ext = [0u8; 2];
                self.stream.read_exact(&mut ext).await.unwrap();
                len = u64::from(u16::from_be_bytes(ext));
            } else if len == 127 {
                let mut ext = [0u8; 8];
                self.stream.read_exact(&mut ext).await.unwrap();
                len = u64::from_be_bytes(ext);
            }

            let mask_key = if masked {
                let mut key = [0u8; 4];
                self.stream.read_exact(&mut key).await.unwrap();
                Some(key)
            } else {
                None
            };

            let mut payload = vec![0u8; len as usize];
            if len > 0 {
                self.stream.read_exact(&mut payload).await.unwrap();
            }
            if let Some(key) = mask_key {
                for (i, b) in payload.iter_mut().enumerate() {
                    *b ^= key[i % 4];
                }
            }

            match opcode {
                0x1 => return String::from_utf8(payload).expect("text frame must be UTF-8"),
                0x8 => panic!("WebSocket connection closed by server while awaiting an event"),
                0x9 | 0xA => continue, // ping/pong: ignore and read the next frame
                other => panic!("unexpected WebSocket opcode: {other:#x}"),
            }
        }
    }
}
