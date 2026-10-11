//! The window procedure: hands each message to [`App`], and keeps the window and the keep-awake
//! request consistent when a message arrives while the app is already busy.

use std::cell::RefCell;
use std::sync::atomic::{AtomicU32, Ordering};
use std::time::Duration;

use windows_sys::Win32::Foundation::{HWND, LPARAM, LRESULT, WPARAM};
use windows_sys::Win32::UI::WindowsAndMessaging::{
    DefWindowProcW, DestroyWindow, PostMessageW, PostQuitMessage, WM_APP, WM_CLOSE, WM_DESTROY,
    WM_DISPLAYCHANGE, WM_ENDSESSION, WM_POWERBROADCAST, WM_QUERYENDSESSION, WM_SETTINGCHANGE,
    WM_WTSSESSION_CHANGE,
};

use super::{App, Outcome, release_request};
use crate::logging;
use crate::platform::messages::{WM_APP_END, WM_ENVIRONMENT_CHANGED};
use crate::platform::{menu, tray, wide, window};

/// `TaskbarCreated` is registered at run time. The procedure reads it here rather than from the
/// app, because it must recognize the message even while the app is busy.
static TASKBAR_CREATED: AtomicU32 = AtomicU32::new(0);
pub(super) const THEME_CHANGED: usize = 1;
pub(super) const DISPLAY_CHANGED: usize = 2;

/// Registers `TaskbarCreated`, which a restarted Explorer broadcasts.
pub(super) fn register_taskbar_created(window: HWND) {
    TASKBAR_CREATED.store(
        window::register_message(window, "TaskbarCreated"),
        Ordering::Relaxed,
    );
}

/// The `TaskbarCreated` message, or 0 before it is registered.
pub(super) fn taskbar_created() -> u32 {
    TASKBAR_CREATED.load(Ordering::Relaxed)
}

pub(super) unsafe extern "system" fn window_proc(
    window: HWND,
    message: u32,
    wparam: WPARAM,
    lparam: LPARAM,
) -> LRESULT {
    let data = window::user_data(window) as *const RefCell<App>;
    if data.is_null() {
        // SAFETY: default handling for messages that arrive before or after the app exists.
        return unsafe { DefWindowProcW(window, message, wparam, lparam) };
    }
    // SAFETY: set in `launch` to a RefCell that outlives the message loop and is cleared after it.
    let cell = unsafe { &*data };
    let outcome = match cell.try_borrow_mut() {
        Ok(mut app) => app.on_message(message, wparam, lparam),
        // Windows re-entered the procedure from inside a call made while handling another
        // message (a cross-process call such as Shell_NotifyIcon dispatches sent messages).
        Err(_) => return reentered(window, message, wparam, lparam),
    };
    match outcome {
        Outcome::Handled(result) => result,
        // SAFETY: default handling.
        Outcome::Default => unsafe { DefWindowProcW(window, message, wparam, lparam) },
        Outcome::ShowMenu(x, y) => {
            show_menu(cell, window, x, y);
            0
        }
        Outcome::Destroy => {
            // SAFETY: our own window; WM_DESTROY re-enters this procedure after the borrow ended.
            unsafe { DestroyWindow(window) };
            0
        }
    }
}

/// What a settings or display broadcast means for Awake, as `THEME_CHANGED | DISPLAY_CHANGED`.
pub(super) fn environment_change(message: u32, wparam: WPARAM, lparam: LPARAM) -> usize {
    match message {
        WM_DISPLAYCHANGE => DISPLAY_CHANGED,
        // lParam names the changed section; switching between light and dark is this one.
        WM_SETTINGCHANGE if lparam != 0 => {
            // SAFETY: for WM_SETTINGCHANGE a non-null lParam is a NUL-terminated string, valid
            // while the message is being handled.
            let theme = unsafe { wide::nul_terminated_eq(lparam as *const u16, "ImmersiveColorSet") };
            if theme { THEME_CHANGED } else { 0 }
        }
        WM_ENVIRONMENT_CHANGED => wparam,
        _ => 0,
    }
}

/// Handles a message that arrived while the app was busy. Nothing that ends the window or
/// changes the session may fall through to default handling, or Awake could end up without a
/// window while still holding the keep-awake request.
fn reentered(window: HWND, message: u32, wparam: WPARAM, lparam: LPARAM) -> LRESULT {
    let repost = |result: LRESULT| {
        // SAFETY: re-posting to our own window, to be handled once the app is free.
        unsafe { PostMessageW(window, message, wparam, lparam) };
        result
    };
    match message {
        WM_DESTROY => {
            // While the window still exists, as in `App::on_message`.
            tray::remove_icon(window);
            // SAFETY: ends this thread's message loop; finish() cleans up afterwards.
            unsafe { PostQuitMessage(0) };
            0
        }
        WM_QUERYENDSESSION | WM_POWERBROADCAST => repost(1),
        // Windows may end the process as soon as this returns, so clean up right here, then
        // close once the app is free: after a Restart Manager request the session goes on.
        WM_ENDSESSION if wparam != 0 => {
            release_request();
            tray::remove_icon(window);
            logging::flush(Duration::from_millis(500));
            // SAFETY: posting to our own window.
            unsafe { PostMessageW(window, WM_CLOSE, 0, 0) };
            0
        }
        // The settings text in lParam is only valid during the call, so post what it means instead.
        WM_SETTINGCHANGE | WM_DISPLAYCHANGE => {
            let change = environment_change(message, wparam, lparam);
            if change != 0 {
                // SAFETY: posting to our own window.
                unsafe { PostMessageW(window, WM_ENVIRONMENT_CHANGED, change, 0) };
            }
            0
        }
        WM_CLOSE | WM_WTSSESSION_CHANGE => repost(0),
        m if (WM_APP..WM_APP_END).contains(&m) || m == taskbar_created() => repost(0),
        // SAFETY: default handling for everything else.
        _ => unsafe { DefWindowProcW(window, message, wparam, lparam) },
    }
}

/// Runs the menu's modal loop without holding the app borrow, so timers and waiter messages
/// keep flowing while it is open.
fn show_menu(cell: &RefCell<App>, window: HWND, x: i32, y: i32) {
    let Ok((items, presets, right_to_left)) = cell.try_borrow().map(|app| {
        let (items, presets) = app.menu_model();
        (items, presets, app.right_to_left)
    }) else {
        return;
    };
    if items.is_empty() {
        return;
    }
    let popup = match menu::build(&items) {
        Ok(popup) => popup,
        Err(error) => {
            log::error!("Could not build the context menu: {error}");
            return;
        }
    };
    let command = menu::track(&popup, window, x, y, right_to_left);
    drop(popup);
    let outcome = match cell.try_borrow_mut() {
        Ok(mut app) => {
            app.tray.return_focus();
            if command == 0 {
                Outcome::Handled(0)
            } else {
                app.on_menu_command(command, &presets)
            }
        }
        Err(_) => return,
    };
    if outcome == Outcome::Destroy {
        // SAFETY: our own window.
        unsafe { DestroyWindow(window) };
    }
}
