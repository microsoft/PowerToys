//! PowerToys Awake: keeps the computer awake on demand, from the PowerToys settings or from the
//! command line.

mod app;
mod bridge;
#[cfg(feature = "test-hooks")]
mod hook_names;
mod hooks;
mod log_lines;
mod logging;
mod platform;

use std::process::ExitCode;
use std::time::Duration;

/// The PowerToys version from src/Version.props, shaped like `v0.97.1`.
pub const VERSION: &str = env!("AWAKE_PRODUCT_VERSION");

/// Awake's own build codename, logged to pinpoint issues beyond the PowerToys version.
/// Format: `CODENAME_MMDDYYYY`, the date of the last change before the pull request.
pub const BUILD_ID: &str = "MENDICANT_10112026";

fn main() -> ExitCode {
    std::panic::set_hook(Box::new(|info| {
        // Release builds abort right after this, and Windows drops the keep-awake request with the
        // process, so a crashed Awake never holds the computer awake. Logging from the hook cannot
        // deadlock; see the logging module docs.
        let message = format!("Awake stopped unexpectedly: {info}");
        logging::emergency(&message);
        log::error!("{message}");
        // The panic never reaches the end of `app::run`, which hands the shell its console mode back.
        platform::console::restore_mode();
        logging::flush(Duration::from_millis(500));
    }));
    ExitCode::from(u8::try_from(app::run()).unwrap_or(1))
}
