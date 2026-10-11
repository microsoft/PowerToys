//! Flags for `SetThreadExecutionState`.
//!
//! The values repeat the Windows headers so this crate needs no Windows dependency; a test in the
//! `awake` binary checks them against windows-sys.

/// Keeps the system from sleeping.
pub const ES_SYSTEM_REQUIRED: u32 = 0x0000_0001;
/// Keeps the display from turning off.
pub const ES_DISPLAY_REQUIRED: u32 = 0x0000_0002;
/// Makes the request last until it is changed, instead of only resetting the idle timer.
pub const ES_CONTINUOUS: u32 = 0x8000_0000;

/// Releases every request this thread made.
pub const RELEASED: u32 = ES_CONTINUOUS;

/// Flags for an active keep-awake session. The display is only held while the session is
/// unlocked, so the lock screen can still turn the monitor off.
pub(crate) const fn keep_awake(keep_display_on: bool, screen_locked: bool) -> u32 {
    if keep_display_on && !screen_locked {
        ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED | ES_CONTINUOUS
    } else {
        ES_SYSTEM_REQUIRED | ES_CONTINUOUS
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    // The same cases the .NET implementation tested, plus the fourth combination it missed.
    #[test]
    fn display_on_and_unlocked_holds_the_display() {
        assert_eq!(
            keep_awake(true, false),
            ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED | ES_CONTINUOUS
        );
    }

    #[test]
    fn display_on_but_locked_releases_the_display() {
        assert_eq!(keep_awake(true, true), ES_SYSTEM_REQUIRED | ES_CONTINUOUS);
    }

    #[test]
    fn display_off_never_holds_the_display() {
        assert_eq!(keep_awake(false, false), ES_SYSTEM_REQUIRED | ES_CONTINUOUS);
        assert_eq!(keep_awake(false, true), ES_SYSTEM_REQUIRED | ES_CONTINUOUS);
    }
}
