//! The keep-awake request itself.

use windows_sys::Win32::System::Power::SetThreadExecutionState;

/// Sets this thread's execution state. With `ES_CONTINUOUS` the request lasts until changed or
/// until the thread exits, so it must always be called from the same long-lived thread.
pub fn set_execution_state(flags: u32) -> bool {
    // SAFETY: plain value call; Windows validates the flags.
    unsafe { SetThreadExecutionState(flags) != 0 }
}
