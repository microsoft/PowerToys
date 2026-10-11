//! Thin, safe wrappers over the Windows APIs Awake uses. Each module owns one concern and
//! keeps its `unsafe` blocks small and documented.

pub mod clock;
pub mod console;
pub mod instance;
pub mod locale;
pub mod menu;
pub mod paths;
pub mod power;
pub mod process;
pub mod session;
pub mod settings_store;
pub mod theme;
pub mod tray;
pub mod waiter;
pub mod wide;
pub mod window;

use std::io;

/// The calling thread's last Win32 error.
pub fn last_error() -> io::Error {
    io::Error::last_os_error()
}
