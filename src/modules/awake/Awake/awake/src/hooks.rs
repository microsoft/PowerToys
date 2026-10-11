//! Knobs for the end-to-end tests, compiled in only with the `test-hooks` feature, so the tests
//! can reach paths a healthy Awake never takes without waiting out the real timeouts. The
//! environment variables they read are named in `hook_names`, which the test kit shares.

use std::time::Duration;

/// How long a PowerToys start waits for the previous instance to exit.
#[cfg(feature = "test-hooks")]
pub fn handover(default: Duration) -> Duration {
    millis_from_env(crate::hook_names::HANDOVER_MS).unwrap_or(default)
}

#[cfg(not(feature = "test-hooks"))]
pub fn handover(default: Duration) -> Duration {
    default
}

/// How long a stop request may take before the watchdog ends the process.
#[cfg(feature = "test-hooks")]
pub fn watchdog(default: Duration) -> Duration {
    millis_from_env(crate::hook_names::WATCHDOG_MS).unwrap_or(default)
}

#[cfg(not(feature = "test-hooks"))]
pub fn watchdog(default: Duration) -> Duration {
    default
}

#[cfg(feature = "test-hooks")]
fn millis_from_env(name: &str) -> Option<Duration> {
    std::env::var(name)
        .ok()
        .and_then(|value| value.parse().ok())
        .map(Duration::from_millis)
}

/// Under test, makes the window thread stop responding as it starts to exit, the way a hung
/// shell call would, so the watchdog has to end the process.
#[cfg(feature = "test-hooks")]
pub fn hang_on_exit() {
    if std::env::var_os(crate::hook_names::HANG_ON_EXIT).is_some() {
        log::warn!("Test hook: the window thread stops responding.");
        loop {
            std::thread::sleep(Duration::from_hours(1));
        }
    }
}

#[cfg(not(feature = "test-hooks"))]
pub fn hang_on_exit() {}
