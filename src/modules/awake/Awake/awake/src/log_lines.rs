//! Log lines the end-to-end tests wait for or count. The test kit includes this file, so a line
//! reworded here changes what the tests look for as well; reword one of these lines anywhere
//! else and the tests time out instead of failing to compile.
//!
//! The constants are the fixed part of a line; the details, where a line has any, follow it.

use awake_core::mode::Mode;

/// The runner, or a test, signaled the exit event.
pub const EXIT_EVENT_SIGNALED: &str = "Exit event signaled.";
/// The Restart Manager asked Awake to close.
pub const CLOSING_FOR_UPDATE: &str = "Asked to close so files can be updated.";
/// Followed by the changes written to `settings.json`.
pub const SAVED: &str = "Saved settings:";
/// Followed by the error that stopped a save for good.
pub const SAVE_FAILED: &str = "Could not save settings:";
/// Followed by why the file could not be read; the session goes on unchanged.
pub const SETTINGS_UNREADABLE: &str = "Keeping the current mode; the settings file is unreadable:";
/// Logged once until the file appears again.
pub const SETTINGS_REMOVED: &str =
    "The settings file was removed; keeping the current mode until it is written again.";
/// Followed by the time the watchdog allowed.
pub const ENDING_HUNG_PROCESS: &str = "Ending the process: the window thread did not shut down within";
/// Followed by the arguments of the detached process.
pub const CONTINUING_DETACHED: &str = "Continuing detached:";
/// The detached process confirmed that its session runs.
pub const HANDOFF_CONFIRMED: &str = "The detached session started.";
/// Starts every [`entered`] line. The space keeps it from matching `ModeEntered(..)` in the
/// effects other lines list.
pub const ENTERED: &str = "Entered ";

/// The session entered `mode`.
pub fn entered(mode: Mode) -> String {
    format!("{ENTERED}{mode:?}.")
}

/// Windows accepted these execution state flags.
pub fn execution_state_set(flags: u32) -> String {
    format!("Execution state set to {flags:#x}.")
}

/// The settings file asked for a mode Awake does not know; the session goes on unchanged.
pub fn unknown_mode(value: i64) -> String {
    format!("Ignoring unknown mode {value} in the settings file.")
}

/// Ctrl+C (0), Ctrl+Break (1), or the console closing, signing out or shutting down.
pub fn console_control_event(kind: usize) -> String {
    format!("Console control event {kind}.")
}

/// The last line Awake writes.
pub fn exiting(code: i32) -> String {
    format!("Exiting with code {code}.")
}
