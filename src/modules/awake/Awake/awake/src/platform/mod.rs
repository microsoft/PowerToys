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

/// A failed HRESULT as an error. HRESULTs and Win32 error codes are different number spaces, so
/// only a wrapped Win32 code (FACILITY_WIN32) becomes an OS error; anything else keeps its
/// HRESULT in the message instead of being misread as an unrelated Win32 code.
pub fn hresult_error(hr: i32, call: &str) -> io::Error {
    const FACILITY_WIN32: u32 = 7;
    let bits = hr as u32;
    if (bits >> 16) & 0x1FFF == FACILITY_WIN32 {
        io::Error::from_raw_os_error((bits & 0xFFFF) as i32)
    } else {
        io::Error::other(format!("{call} failed with HRESULT {bits:#010x}"))
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn win32_hresults_become_os_errors() {
        // HRESULT_FROM_WIN32(ERROR_FILE_NOT_FOUND)
        let error = hresult_error(0x8007_0002_u32 as i32, "SHGetKnownFolderPath");
        assert_eq!(error.raw_os_error(), Some(2));
        assert_eq!(error.kind(), io::ErrorKind::NotFound);
    }

    #[test]
    fn other_hresults_keep_their_value() {
        // E_FAIL belongs to FACILITY_NULL; read as a Win32 code it would be nonsense.
        let error = hresult_error(0x8000_4005_u32 as i32, "SHGetKnownFolderPath");
        assert_eq!(error.raw_os_error(), None);
        assert_eq!(
            error.to_string(),
            "SHGetKnownFolderPath failed with HRESULT 0x80004005"
        );
    }
}
