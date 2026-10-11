//! The hidden window that receives tray, power, session and Explorer messages.
//!
//! It is a top-level window rather than a message-only one because broadcasts such as
//! `TaskbarCreated` and `WM_POWERBROADCAST` only reach top-level windows.

use std::{io, mem, ptr};

use windows_sys::Win32::Foundation::{HWND, WPARAM};
use windows_sys::Win32::System::LibraryLoader::GetModuleHandleW;
use windows_sys::Win32::UI::WindowsAndMessaging::{
    CHANGEFILTERSTRUCT, ChangeWindowMessageFilterEx, CreateWindowExW, DispatchMessageW, GWLP_USERDATA,
    GetMessageW, GetWindowLongPtrW, KillTimer, MSG, MSGFLT_ALLOW, RegisterClassExW, RegisterWindowMessageW,
    SetTimer, SetWindowLongPtrW, TranslateMessage, WNDCLASSEXW, WNDPROC, WS_EX_TOOLWINDOW, WS_OVERLAPPED,
};

use super::{last_error, wide::wide};

/// Same class name and title as the .NET version.
const CLASS_NAME: &str = "Awake.MessageWindow";
const TITLE: &str = "PowerToys Awake";

pub fn create(window_proc: WNDPROC) -> io::Result<HWND> {
    let class_name = wide(CLASS_NAME);
    let title = wide(TITLE);
    // SAFETY: null asks for this executable's module handle.
    let instance = unsafe { GetModuleHandleW(ptr::null()) };
    let class = WNDCLASSEXW {
        cbSize: mem::size_of::<WNDCLASSEXW>() as u32,
        lpfnWndProc: window_proc,
        hInstance: instance,
        lpszClassName: class_name.as_ptr(),
        // SAFETY: the remaining fields are optional and zero means "none".
        ..unsafe { mem::zeroed() }
    };
    // SAFETY: `class` and the name it points to are valid for the call.
    if unsafe { RegisterClassExW(&class) } == 0 {
        return Err(last_error());
    }
    // SAFETY: the class was registered above; the window is never shown.
    let window = unsafe {
        CreateWindowExW(
            WS_EX_TOOLWINDOW,
            class_name.as_ptr(),
            title.as_ptr(),
            WS_OVERLAPPED,
            0,
            0,
            0,
            0,
            ptr::null_mut(),
            ptr::null_mut(),
            instance,
            ptr::null(),
        )
    };
    if window.is_null() {
        Err(last_error())
    } else {
        Ok(window)
    }
}

/// Registers `name` and lets it through UIPI, so Explorer can reach Awake when it runs elevated.
pub fn register_message(window: HWND, name: &str) -> u32 {
    let name = wide(name);
    // SAFETY: NUL-terminated name.
    let message = unsafe { RegisterWindowMessageW(name.as_ptr()) };
    allow_message(window, message);
    message
}

pub fn allow_message(window: HWND, message: u32) {
    if message == 0 {
        return;
    }
    let mut status = CHANGEFILTERSTRUCT {
        cbSize: mem::size_of::<CHANGEFILTERSTRUCT>() as u32,
        ExtStatus: 0,
    };
    // SAFETY: our own window and a valid struct.
    unsafe { ChangeWindowMessageFilterEx(window, message, MSGFLT_ALLOW, &mut status) };
}

/// Stores the application pointer the window procedure reads back.
///
/// # Safety
/// `data` must stay valid until it is cleared again or the window is destroyed.
pub unsafe fn set_user_data(window: HWND, data: isize) {
    // SAFETY: guaranteed by the caller.
    unsafe { SetWindowLongPtrW(window, GWLP_USERDATA, data) };
}

pub fn user_data(window: HWND) -> isize {
    // SAFETY: reading our own window's slot.
    unsafe { GetWindowLongPtrW(window, GWLP_USERDATA) }
}

/// Returns false when Windows is out of timers.
pub fn set_timer(window: HWND, id: usize, millis: u32) -> bool {
    // SAFETY: timer on our own window; no callback, so it arrives as WM_TIMER.
    unsafe { SetTimer(window, id, millis, None) != 0 }
}

pub fn kill_timer(window: HWND, id: usize) {
    // SAFETY: killing a timer that may not exist is harmless.
    unsafe { KillTimer(window, id) };
}

/// Pumps messages until `WM_QUIT`.
pub fn run_message_loop() {
    // SAFETY: MSG is plain data.
    let mut message: MSG = unsafe { mem::zeroed() };
    loop {
        // SAFETY: `message` is valid for writes; null window means "any window on this thread".
        let result = unsafe { GetMessageW(&mut message, ptr::null_mut(), 0, 0) };
        if result == 0 || result == -1 {
            if result == -1 {
                log::error!("GetMessage failed: {}", last_error());
            }
            return;
        }
        // SAFETY: dispatching a message we just received.
        unsafe {
            TranslateMessage(&message);
            DispatchMessageW(&message);
        }
    }
}

pub fn loword(value: usize) -> u32 {
    (value & 0xFFFF) as u32
}

/// Signed x and y packed into a WPARAM, as `GET_X_LPARAM`/`GET_Y_LPARAM` read them.
pub fn point_from_wparam(value: WPARAM) -> (i32, i32) {
    (
        i32::from((value & 0xFFFF) as u16 as i16),
        i32::from(((value >> 16) & 0xFFFF) as u16 as i16),
    )
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn coordinates_are_signed() {
        assert_eq!(point_from_wparam(0x0010_0020), (0x20, 0x10));
        assert_eq!(
            point_from_wparam(0xFFFF_FFFE),
            (-2, -1),
            "monitors left of or above the primary one"
        );
    }
}
