//! Knobs for the end-to-end tests, compiled in only with the `test-hooks` feature, so the tests
//! can reach paths a healthy Awake never takes without waiting out the real timeouts.

use std::time::Duration;

/// `default`, or the milliseconds in the environment variable `name` under test.
#[cfg(feature = "test-hooks")]
pub fn duration(name: &str, default: Duration) -> Duration {
    std::env::var(name)
        .ok()
        .and_then(|value| value.parse().ok())
        .map_or(default, Duration::from_millis)
}

#[cfg(not(feature = "test-hooks"))]
pub fn duration(_: &str, default: Duration) -> Duration {
    default
}

/// Under test, makes the window thread stop responding as it starts to exit, the way a hung
/// shell call would, so the watchdog has to end the process.
#[cfg(feature = "test-hooks")]
pub fn hang_on_exit() {
    if std::env::var_os("POWERTOYS_AWAKE_TEST_HANG_ON_EXIT").is_some() {
        log::warn!("Test hook: the window thread stops responding.");
        loop {
            std::thread::sleep(Duration::from_hours(1));
        }
    }
}

#[cfg(not(feature = "test-hooks"))]
pub fn hang_on_exit() {}
