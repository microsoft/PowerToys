//! The keep-awake request itself.

use windows_sys::Win32::System::Power::SetThreadExecutionState;

/// Sets this thread's execution state. With `ES_CONTINUOUS` the request lasts until changed or
/// until the thread exits, so it must always be called from the same long-lived thread.
pub fn set_execution_state(flags: u32) -> bool {
    // SAFETY: plain value call; Windows validates the flags.
    unsafe { SetThreadExecutionState(flags) != 0 }
}

#[cfg(test)]
mod tests {
    use awake_core::execution_state::{ES_CONTINUOUS, ES_DISPLAY_REQUIRED, ES_SYSTEM_REQUIRED};
    use awake_core::tray_model::ids;
    use windows_sys::Win32::System::Power as power;
    use windows_sys::Win32::UI::WindowsAndMessaging::WM_USER;

    // awake-core stays free of Windows dependencies, so it repeats these values; this pins the
    // copies to the real headers.
    #[test]
    fn core_execution_state_flags_match_windows() {
        assert_eq!(ES_SYSTEM_REQUIRED, power::ES_SYSTEM_REQUIRED);
        assert_eq!(ES_DISPLAY_REQUIRED, power::ES_DISPLAY_REQUIRED);
        assert_eq!(ES_CONTINUOUS, power::ES_CONTINUOUS);
    }

    // The .NET implementation numbered its menu commands from `WM_USER`; the core keeps the same
    // numbers so nothing that relied on them breaks.
    #[test]
    fn menu_ids_keep_their_wm_user_offsets() {
        let offset = |id: u16| u32::from(id) - WM_USER;
        assert_eq!(offset(ids::DISPLAY), 2);
        assert_eq!(offset(ids::OFF), 3);
        assert_eq!(offset(ids::INDEFINITE), 4);
        assert_eq!(offset(ids::EXPIRABLE), 5);
        assert_eq!(offset(ids::EXIT), 100);
        assert_eq!(offset(ids::FIRST_PRESET), 101);
    }
}
