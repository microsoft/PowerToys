//! Whether the session is locked. Changes arrive as `WM_WTSSESSION_CHANGE` on the tray window.

use std::{mem, ptr};

use windows_sys::Win32::Foundation::HWND;
use windows_sys::Win32::System::RemoteDesktop::{
    NOTIFY_FOR_THIS_SESSION, WTS_CURRENT_SERVER_HANDLE, WTS_CURRENT_SESSION, WTSFreeMemory, WTSINFOEXW,
    WTSQuerySessionInformationW, WTSRegisterSessionNotification, WTSSessionInfoEx,
    WTSUnRegisterSessionNotification,
};

pub const WTS_SESSION_LOCK: usize = 0x7;
pub const WTS_SESSION_UNLOCK: usize = 0x8;
const WTS_SESSIONSTATE_LOCK: i32 = 0;

/// Asks Windows directly. When it cannot answer, assume unlocked, as the .NET version did.
pub fn is_locked() -> bool {
    let mut buffer = ptr::null_mut();
    let mut bytes = 0u32;
    // SAFETY: out-pointers are valid; the buffer is freed below.
    let ok = unsafe {
        WTSQuerySessionInformationW(
            WTS_CURRENT_SERVER_HANDLE,
            WTS_CURRENT_SESSION,
            WTSSessionInfoEx,
            &mut buffer,
            &mut bytes,
        )
    } != 0;
    if !ok || buffer.is_null() {
        return false;
    }
    let locked = if bytes as usize >= mem::size_of::<WTSINFOEXW>() {
        // SAFETY: Windows returned a WTSINFOEXW of at least that size; level 1 is the only level.
        unsafe {
            let info = &*(buffer as *const WTSINFOEXW);
            info.Level == 1 && info.Data.WTSInfoExLevel1.SessionFlags == WTS_SESSIONSTATE_LOCK
        }
    } else {
        false
    };
    // SAFETY: buffer came from WTSQuerySessionInformationW.
    unsafe { WTSFreeMemory(buffer as _) };
    locked
}

pub fn register(window: HWND) -> bool {
    // SAFETY: `window` is a live window owned by this thread.
    unsafe { WTSRegisterSessionNotification(window, NOTIFY_FOR_THIS_SESSION) != 0 }
}

pub fn unregister(window: HWND) {
    // SAFETY: as above.
    unsafe { WTSUnRegisterSessionNotification(window) };
}
