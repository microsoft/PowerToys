//! The environment variables the `test-hooks` feature reads. The test kit includes this file, so
//! the tests and the binary cannot disagree on a name. Shipped builds read none of them.

/// Replaces `%LOCALAPPDATA%\Microsoft\PowerToys`, so tests never touch a real installation.
pub const DATA_DIR: &str = "POWERTOYS_AWAKE_DATA_DIR";
/// Appended to the names of the single-instance mutex and the exit event; Awake keeps only ASCII
/// letters, digits and hyphens from it.
pub const OBJECT_SUFFIX: &str = "POWERTOYS_AWAKE_OBJECT_SUFFIX";
/// Milliseconds a PowerToys start waits for the previous instance to exit, instead of 5 seconds.
pub const HANDOVER_MS: &str = "POWERTOYS_AWAKE_TEST_HANDOVER_MS";
/// Milliseconds a stop request may take before the watchdog ends the process, instead of 10
/// seconds.
pub const WATCHDOG_MS: &str = "POWERTOYS_AWAKE_TEST_WATCHDOG_MS";
/// Present: the window thread stops responding as it starts to exit.
pub const HANG_ON_EXIT: &str = "POWERTOYS_AWAKE_TEST_HANG_ON_EXIT";
