//! Every private window message and timer id the tray window uses, in one place.
//!
//! All of them arrive at the same window procedure, so two equal values would route one
//! module's message to another module's handler. The checks below stop that at compile time.

use windows_sys::Win32::UI::WindowsAndMessaging::WM_APP;

/// From the waiter thread; `wParam` is a `WaitEvent`.
pub const WM_WAITER: u32 = WM_APP + 1;
/// Notification-area icon callbacks.
pub const WM_TRAY: u32 = WM_APP + 2;
/// From the console control handler; `wParam` is the control event.
pub const WM_CONSOLE_CTRL: u32 = WM_APP + 3;
/// Theme or display settings changed while the app was busy; `wParam` holds the change flags.
pub const WM_ENVIRONMENT_CHANGED: u32 = WM_APP + 4;
/// The end of the private `WM_APP` range; registered messages such as `TaskbarCreated` start here.
pub const WM_APP_END: u32 = 0xC000;

/// The live countdown of a timed session.
pub const TIMER_TICK: usize = 1;
/// Debounces and retries reading `settings.json`.
pub const TIMER_RELOAD: usize = 2;
/// Retries adding the notification-area icon.
pub const TIMER_TRAY_RETRY: usize = 3;
/// Retries saving `settings.json`.
pub const TIMER_WRITE: usize = 4;
/// Retries registering for session lock notifications.
pub const TIMER_SESSION: usize = 5;

const MESSAGES: [u32; 4] = [WM_WAITER, WM_TRAY, WM_CONSOLE_CTRL, WM_ENVIRONMENT_CHANGED];
const TIMERS: [usize; 5] = [
    TIMER_TICK,
    TIMER_RELOAD,
    TIMER_TRAY_RETRY,
    TIMER_WRITE,
    TIMER_SESSION,
];

const fn distinct(values: &[usize]) -> bool {
    let mut i = 0;
    while i < values.len() {
        let mut j = i + 1;
        while j < values.len() {
            if values[i] == values[j] {
                return false;
            }
            j += 1;
        }
        i += 1;
    }
    true
}

const fn widen(values: [u32; 4]) -> [usize; 4] {
    let mut wide = [0; 4];
    let mut i = 0;
    while i < values.len() {
        wide[i] = values[i] as usize;
        i += 1;
    }
    wide
}

const fn in_private_range(values: [u32; 4]) -> bool {
    let mut i = 0;
    while i < values.len() {
        if values[i] < WM_APP || values[i] >= WM_APP_END {
            return false;
        }
        i += 1;
    }
    true
}

const _: () = assert!(distinct(&widen(MESSAGES)), "two private messages share a number");
const _: () = assert!(
    in_private_range(MESSAGES),
    "private messages must stay in the WM_APP range"
);
const _: () = assert!(distinct(&TIMERS), "two timers share an id");
