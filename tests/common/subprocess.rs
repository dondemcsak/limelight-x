//! Subprocess helpers for server-lifecycle tests (spec/bdd-api.md §1).
//! `cmd_serve`/`main`/`run` live in the `llx` binary target, not the
//! `limelight_x` library, so port-in-use / missing-API-key fail-fast
//! behavior can only be observed by spawning the real compiled binary.
//! These tests must never let the spawned process reach a real model call —
//! only `/explain` (never invokes the adapter) or exit code/stderr are ever
//! asserted on (CLAUDE.md §6).

use std::io::{BufRead, BufReader};
use std::process::{Child, Command, Stdio};

/// Kills the wrapped child on drop (including on test panic/unwind), so a
/// failed assertion never leaks a live `llx serve` process holding a port.
pub struct ChildGuard(pub Child);

impl Drop for ChildGuard {
    fn drop(&mut self) {
        let _ = self.0.kill();
        let _ = self.0.wait();
    }
}

/// Spawns `llx serve <extra_args>` with a dummy `ANTHROPIC_API_KEY`, blocks
/// until its startup line is written to stdout, and returns the guarded
/// child alongside that line.
pub fn spawn_llx_serve(extra_args: &[&str]) -> (ChildGuard, String) {
    let mut child = Command::new(env!("CARGO_BIN_EXE_llx"))
        .arg("serve")
        .args(extra_args)
        .env("ANTHROPIC_API_KEY", "sk-test-dummy-key-not-a-real-key")
        .stdout(Stdio::piped())
        .stderr(Stdio::piped())
        .spawn()
        .expect("failed to spawn the llx binary");

    let stdout = child.stdout.take().expect("child stdout was not piped");
    let mut reader = BufReader::new(stdout);
    let mut line = String::new();
    reader
        .read_line(&mut line)
        .expect("failed to read llx serve's startup line");

    (ChildGuard(child), line)
}
