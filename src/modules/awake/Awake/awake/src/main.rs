//! PowerToys Awake: keeps the computer awake on demand, from the PowerToys settings or from the
//! command line.

mod app;
mod bridge;
mod hooks;
mod logging;
mod platform;

use std::process::ExitCode;
use std::time::Duration;

/// `v0.97.1`, from src/Version.props.
pub const VERSION: &str = env!("AWAKE_PRODUCT_VERSION");

/// Awake's own build codename, logged to pinpoint issues beyond the PowerToys version.
/// Format: CODENAME_MMDDYYYY, the date of the last change before the pull request.
pub const BUILD_ID: &str = "MENDICANT_10112026";

fn main() -> ExitCode {
    std::panic::set_hook(Box::new(|info| {
        // The process ends right after this (release builds abort on panic), and Windows then
        // drops the keep-awake request, so the computer is never held awake by a crashed Awake.
        // Awake's code takes no lock on the log path. The log channel's own wake-up lock is held
        // only briefly inside std, by code that cannot panic, so a panic raised inside a log call
        // cannot re-enter it and deadlock the hook before the process ends.
        let message = format!("Awake stopped unexpectedly: {info}");
        logging::emergency(&message);
        log::error!("{message}");
        logging::flush(Duration::from_millis(500));
    }));
    ExitCode::from(u8::try_from(app::run()).unwrap_or(1))
}
