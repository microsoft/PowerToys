//! The notification-area icon.
//!
//! Shell calls never block the window thread: when Explorer is not ready, a timer retries later,
//! and a restarted Explorer announces itself with `TaskbarCreated`, which re-adds the icon.

use std::{mem, ptr};

use windows_sys::Win32::Foundation::HWND;
use windows_sys::Win32::System::LibraryLoader::GetModuleHandleW;
use windows_sys::Win32::UI::Controls::{LIM_SMALL, LoadIconMetric};
use windows_sys::Win32::UI::Shell::{
    NIF_ICON, NIF_MESSAGE, NIF_SHOWTIP, NIF_TIP, NIM_ADD, NIM_DELETE, NIM_MODIFY, NIM_SETFOCUS,
    NIM_SETVERSION, NOTIFYICON_VERSION_4, NOTIFYICONDATAW, Shell_NotifyIconW,
};
use windows_sys::Win32::UI::WindowsAndMessaging::{DestroyIcon, HICON, KillTimer, SetTimer, WM_APP};

use super::wide::copy_truncated;

pub const WM_TRAY: u32 = WM_APP + 2;
pub const TIMER_RETRY: usize = 3;
/// Same identifier as before, so Windows keeps the user's "always show" choice.
const ICON_ID: u32 = 1000;
const MAX_RETRIES: u32 = 10;

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum IconKind {
    Indefinite,
    Timed,
    Expirable,
    Passive,
}

impl IconKind {
    /// Resource ids from build.rs. Id 1 is the executable's own icon.
    fn resource_id(self) -> u16 {
        match self {
            IconKind::Indefinite => 101,
            IconKind::Timed => 102,
            IconKind::Expirable => 103,
            IconKind::Passive => 104,
        }
    }

    fn index(self) -> usize {
        self as usize
    }
}

#[derive(Debug)]
pub struct Tray {
    window: HWND,
    icons: [HICON; 4],
    current: Option<(IconKind, String)>,
    added: bool,
    retries: u32,
    /// A retry timer is pending, or retries ran out; changes are only recorded until then.
    waiting: bool,
}

impl Tray {
    pub fn new(window: HWND) -> Self {
        Tray {
            window,
            icons: [ptr::null_mut(); 4],
            current: None,
            added: false,
            retries: 0,
            waiting: false,
        }
    }

    pub fn show(&mut self, kind: IconKind, tooltip: &str) {
        if self
            .current
            .as_ref()
            .is_some_and(|(k, t)| *k == kind && t == tooltip)
            && self.added
        {
            return;
        }
        self.current = Some((kind, tooltip.to_owned()));
        if !self.waiting {
            self.sync();
        }
    }

    /// Explorer restarted, or the taskbar moved to a display with another scale.
    pub fn taskbar_created(&mut self) {
        // A new Explorer starts empty, but clear any icon an old one may still hold.
        self.notify(NIM_DELETE);
        self.added = false;
        self.retries = 0;
        self.waiting = false;
        self.release_icons();
        self.sync();
    }

    pub fn display_changed(&mut self) {
        self.release_icons();
        if self.added {
            self.added = self.notify(NIM_MODIFY);
        }
    }

    pub fn retry(&mut self) {
        // SAFETY: killing a timer on our own window.
        unsafe { KillTimer(self.window, TIMER_RETRY) };
        self.waiting = false;
        self.sync();
    }

    /// Gives keyboard focus back to the notification area after the menu closes.
    pub fn return_focus(&mut self) {
        if self.added {
            self.notify(NIM_SETFOCUS);
        }
    }

    pub fn remove(&mut self) {
        // Deleting an icon that is not there is harmless; leaving one behind is not.
        self.notify(NIM_DELETE);
        self.added = false;
        // SAFETY: as above.
        unsafe { KillTimer(self.window, TIMER_RETRY) };
    }

    fn sync(&mut self) {
        if self.current.is_none() {
            return;
        }
        if self.added && self.notify(NIM_MODIFY) {
            return;
        }
        // Not added yet, or the update failed. Adding fails if Explorer still has the icon from a
        // momentary hiccup, so fall back to updating it in that case.
        self.added = self.notify(NIM_ADD) || self.notify(NIM_MODIFY);
        if self.added {
            self.retries = 0;
            self.notify(NIM_SETVERSION);
        } else if self.retries < MAX_RETRIES {
            let delay = (500u32 << self.retries).min(2000);
            self.retries += 1;
            self.waiting = true;
            log::warn!(
                "The notification area is not ready; retry {} in {delay} ms.",
                self.retries
            );
            // SAFETY: timer on our own window.
            unsafe { SetTimer(self.window, TIMER_RETRY, delay, None) };
        } else {
            self.waiting = true;
            log::error!("Could not add the notification icon; waiting for Explorer to restart.");
        }
    }

    fn notify(&mut self, message: u32) -> bool {
        // SAFETY: NOTIFYICONDATAW is plain data; zero is a valid starting value.
        let mut data: NOTIFYICONDATAW = unsafe { mem::zeroed() };
        data.cbSize = mem::size_of::<NOTIFYICONDATAW>() as u32;
        data.hWnd = self.window;
        data.uID = ICON_ID;
        if message == NIM_SETVERSION {
            data.Anonymous.uVersion = NOTIFYICON_VERSION_4;
        } else if message != NIM_DELETE
            && message != NIM_SETFOCUS
            && let Some((kind, tooltip)) = self.current.clone()
        {
            data.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP | NIF_SHOWTIP;
            data.uCallbackMessage = WM_TRAY;
            data.hIcon = self.icon(kind);
            copy_truncated(&tooltip, &mut data.szTip);
        }
        // SAFETY: `data` is fully initialized for the message being sent.
        unsafe { Shell_NotifyIconW(message, &data) != 0 }
    }

    fn icon(&mut self, kind: IconKind) -> HICON {
        let slot = &mut self.icons[kind.index()];
        if slot.is_null() {
            let mut icon = ptr::null_mut();
            // SAFETY: the module handle of this executable and an integer resource id; LoadIconMetric
            // picks the frame for the current DPI.
            let hr = unsafe {
                LoadIconMetric(
                    GetModuleHandleW(ptr::null()),
                    kind.resource_id() as usize as *const u16,
                    LIM_SMALL,
                    &mut icon,
                )
            };
            if hr < 0 {
                log::error!("Could not load icon {}: {hr:#x}", kind.resource_id());
            }
            *slot = icon;
        }
        *slot
    }

    fn release_icons(&mut self) {
        for icon in &mut self.icons {
            if !icon.is_null() {
                // SAFETY: icons came from LoadIconMetric and are owned by us.
                unsafe { DestroyIcon(*icon) };
                *icon = ptr::null_mut();
            }
        }
    }
}

/// Removes Awake's icon without needing the `Tray`, for shutdown paths that cannot reach it.
pub fn remove_icon(window: HWND) {
    // SAFETY: NOTIFYICONDATAW is plain data; only the window and id identify the icon.
    unsafe {
        let mut data: NOTIFYICONDATAW = mem::zeroed();
        data.cbSize = mem::size_of::<NOTIFYICONDATAW>() as u32;
        data.hWnd = window;
        data.uID = ICON_ID;
        Shell_NotifyIconW(NIM_DELETE, &data);
    }
}

impl Drop for Tray {
    fn drop(&mut self) {
        self.remove();
        self.release_icons();
    }
}
