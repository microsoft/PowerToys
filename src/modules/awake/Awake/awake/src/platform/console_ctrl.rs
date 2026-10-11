//! Ctrl+C, closing the console window, sign-out and shutdown, as the console reports them.
//!
//! Windows calls the handler on a thread of its own. It posts the event to the window thread,
//! which does the cleanup, and then holds Windows off until that cleanup is done.

use std::os::windows::io::IntoRawHandle;
use std::ptr;
use std::sync::atomic::{AtomicPtr, Ordering};

use windows_sys::Win32::Foundation::{HWND, WAIT_TIMEOUT};
use windows_sys::Win32::System::Console::{
    CTRL_BREAK_EVENT, CTRL_C_EVENT, CTRL_CLOSE_EVENT, CTRL_LOGOFF_EVENT, CTRL_SHUTDOWN_EVENT,
    SetConsoleCtrlHandler,
};
use windows_sys::Win32::System::Threading::{SetEvent, WaitForSingleObject};
use windows_sys::Win32::UI::WindowsAndMessaging::PostMessageW;

use super::messages::WM_CONSOLE_CTRL;
use super::watchdog;

static CTRL_WINDOW: AtomicPtr<core::ffi::c_void> = AtomicPtr::new(ptr::null_mut());
static SHUTDOWN_DONE: AtomicPtr<core::ffi::c_void> = AtomicPtr::new(ptr::null_mut());

/// Routes Ctrl+C, closing the console window, sign-out and shutdown to `window`.
pub fn install(window: HWND) {
    CTRL_WINDOW.store(window, Ordering::Release);
    // Intentionally never closed: the handler may wait on it until the process ends.
    if let Ok(done) = super::event::manual_reset(None) {
        SHUTDOWN_DONE.store(done.into_raw_handle(), Ordering::Release);
    }
    // SAFETY: the handler is a plain function valid for the process lifetime.
    unsafe { SetConsoleCtrlHandler(Some(handler), 1) };
}

/// Lets a pending console close or shutdown proceed once Awake has cleaned up.
pub fn signal_shutdown_complete() {
    let done = SHUTDOWN_DONE.load(Ordering::Acquire);
    if !done.is_null() {
        // SAFETY: valid event created in `install`.
        unsafe { SetEvent(done) };
    }
}

unsafe extern "system" fn handler(kind: u32) -> i32 {
    let window = CTRL_WINDOW.load(Ordering::Acquire);
    if window.is_null() {
        return 0;
    }
    // SAFETY: posting to a window owned by another thread is allowed.
    unsafe { PostMessageW(window, WM_CONSOLE_CTRL, kind as usize, 0) };
    match kind {
        // Returning TRUE keeps Windows from ending the process, so if the window thread is hung
        // nothing else would: end it here once the watchdog time has passed.
        CTRL_C_EVENT | CTRL_BREAK_EVENT => {
            let done = SHUTDOWN_DONE.load(Ordering::Acquire);
            let millis = watchdog::limit().as_millis() as u32;
            // SAFETY: valid event handle.
            if !done.is_null() && unsafe { WaitForSingleObject(done, millis) } == WAIT_TIMEOUT {
                watchdog::end_hung_process();
            }
            1
        }
        // Returning from these ends the process, so give the window thread time to clean up.
        CTRL_CLOSE_EVENT | CTRL_LOGOFF_EVENT | CTRL_SHUTDOWN_EVENT => {
            let done = SHUTDOWN_DONE.load(Ordering::Acquire);
            if !done.is_null() {
                // SAFETY: valid event handle.
                unsafe { WaitForSingleObject(done, 4000) };
            }
            1
        }
        _ => 0,
    }
}
