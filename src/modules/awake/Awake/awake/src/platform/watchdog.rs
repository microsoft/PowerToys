//! The last resort when the window thread stops responding.
//!
//! Once a stop has been requested (the exit event, the bound process ending, the deadline of a
//! session that ends the process, or Ctrl+C) the process must be gone within [`limit`]. The
//! waiter thread and the console control handler wait that long and then call
//! [`end_hung_process`]. Windows releases the keep-awake request when the process dies, so the
//! machine is never held awake by a hung Awake.

use std::time::Duration;

use windows_sys::Win32::System::Threading::{GetCurrentProcess, TerminateProcess};

use crate::log_lines;

const LIMIT: Duration = Duration::from_secs(10);

/// How long a stop request may take before the process is ended.
pub fn limit() -> Duration {
    crate::hooks::watchdog(LIMIT)
}

/// Ends the process because the window thread did not act on a stop request in time.
pub fn end_hung_process() -> ! {
    log::error!("{} {:?}.", log_lines::ENDING_HUNG_PROCESS, limit());
    // TerminateProcess skips every normal exit path, so hand the shell back its console mode
    // here; it is one SetConsoleMode call that takes no locks.
    super::console::restore_mode();
    crate::logging::flush(Duration::from_millis(500));
    // SAFETY: terminating our own process.
    unsafe { TerminateProcess(GetCurrentProcess(), 1) };
    std::process::abort()
}
