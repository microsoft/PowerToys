//! The notification-area icon.
//!
//! Shell calls never block the window thread: when Explorer is not ready, a timer retries later,
//! and a restarted Explorer announces itself with `TaskbarCreated`, which re-adds the icon.
//!
//! The Win32 calls sit behind `Shell`, so the retry and update logic around them can be tested
//! without Explorer.

use std::{mem, ptr};

use windows_sys::Win32::Foundation::HWND;
use windows_sys::Win32::System::LibraryLoader::GetModuleHandleW;
use windows_sys::Win32::UI::Controls::{LIM_SMALL, LoadIconMetric};
use windows_sys::Win32::UI::Shell::{
    NIF_ICON, NIF_MESSAGE, NIF_SHOWTIP, NIF_TIP, NIM_ADD, NIM_DELETE, NIM_MODIFY, NIM_SETFOCUS,
    NIM_SETVERSION, NOTIFYICON_VERSION_4, NOTIFYICONDATAW, Shell_NotifyIconW,
};
use windows_sys::Win32::UI::WindowsAndMessaging::{DestroyIcon, HICON, KillTimer, SetTimer};

use super::messages::{TIMER_TRAY_RETRY, WM_TRAY};
use super::wide::copy_truncated;

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

/// The Win32 calls the tray depends on.
pub trait Shell {
    fn notify(&mut self, message: u32, data: &NOTIFYICONDATAW) -> bool;
    /// Loads the icon for the current DPI, or returns the failing HRESULT.
    fn load_icon(&mut self, kind: IconKind) -> Result<HICON, i32>;
    fn destroy_icon(&mut self, icon: HICON);
    /// Returns false when Windows is out of timers.
    fn set_retry_timer(&mut self, delay_ms: u32) -> bool;
    fn kill_retry_timer(&mut self);
}

/// The real shell, for the tray window.
#[derive(Debug)]
pub struct Win32Shell {
    window: HWND,
}

impl Shell for Win32Shell {
    fn notify(&mut self, message: u32, data: &NOTIFYICONDATAW) -> bool {
        // SAFETY: `data` is fully initialized for the message being sent.
        unsafe { Shell_NotifyIconW(message, data) != 0 }
    }

    fn load_icon(&mut self, kind: IconKind) -> Result<HICON, i32> {
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
        if hr < 0 || icon.is_null() {
            Err(hr)
        } else {
            Ok(icon)
        }
    }

    fn destroy_icon(&mut self, icon: HICON) {
        // SAFETY: icons come from `load_icon` and are owned by the tray.
        unsafe { DestroyIcon(icon) };
    }

    fn set_retry_timer(&mut self, delay_ms: u32) -> bool {
        // SAFETY: timer on our own window.
        unsafe { SetTimer(self.window, TIMER_TRAY_RETRY, delay_ms, None) != 0 }
    }

    fn kill_retry_timer(&mut self) {
        // SAFETY: killing a timer on our own window.
        unsafe { KillTimer(self.window, TIMER_TRAY_RETRY) };
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
enum IconSlot {
    Unloaded,
    Loaded(HICON),
    /// Loading failed; not retried until the display or Explorer changes, so a broken resource
    /// or exhausted GDI handles cost one log line, not one a second.
    Failed,
}

/// How long to wait before attempt `retries + 1` to add the icon, or `None` to stop and wait
/// for Explorer to announce itself.
fn retry_delay(retries: u32) -> Option<u32> {
    (retries < MAX_RETRIES).then(|| (500u32 << retries.min(2)).min(2000))
}

/// Whether updating the entry in place would leave Explorer showing another state's image.
/// A modify without `NIF_ICON` keeps the old image, so when the new state's icon is missing and
/// Explorer holds a different one, the entry has to be re-created without an image instead.
fn leaves_stale_image(image: Option<IconKind>, kind: IconKind, have_icon: bool) -> bool {
    !have_icon && image.is_some_and(|image| image != kind)
}

#[derive(Debug)]
pub struct Tray<S: Shell = Win32Shell> {
    window: HWND,
    shell: S,
    icons: [IconSlot; 4],
    current: Option<(IconKind, String)>,
    /// The icon Explorer has. While it matches, updates send only the tooltip: the countdown
    /// changes it every second, and sending the icon makes Explorer copy it each time.
    shown: Option<IconKind>,
    /// The state whose image Explorer displays, at any scale, or `None` when it has no image.
    image: Option<IconKind>,
    added: bool,
    retries: u32,
    /// A retry timer is pending, or retries ran out; changes are only recorded until then.
    waiting: bool,
}

impl Tray {
    pub fn new(window: HWND) -> Self {
        Tray::with_shell(window, Win32Shell { window })
    }
}

impl<S: Shell> Tray<S> {
    fn with_shell(window: HWND, shell: S) -> Self {
        Tray {
            window,
            shell,
            icons: [IconSlot::Unloaded; 4],
            current: None,
            shown: None,
            image: None,
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
        self.shown = None;
        self.retries = 0;
        self.waiting = false;
        self.release_icons();
        self.sync();
    }

    pub fn display_changed(&mut self) {
        self.release_icons();
        self.shown = None;
        if self.added && !self.waiting {
            self.sync();
        }
    }

    pub fn retry(&mut self) {
        self.shell.kill_retry_timer();
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
        self.shown = None;
        self.shell.kill_retry_timer();
    }

    fn sync(&mut self) {
        let Some(kind) = self.current.as_ref().map(|(kind, _)| *kind) else {
            return;
        };
        if self.added && self.shown != Some(kind) {
            let have_icon = self.icon(kind).is_some();
            if leaves_stale_image(self.image, kind, have_icon) {
                // For example, kept awake to off with the off icon failing to load: the entry
                // would go on showing the kept-awake image.
                self.notify(NIM_DELETE);
                self.added = false;
            }
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
        } else if let Some(delay) = retry_delay(self.retries) {
            self.retries += 1;
            self.waiting = true;
            log::warn!(
                "The notification area is not ready; retry {} in {delay} ms.",
                self.retries
            );
            if !self.shell.set_retry_timer(delay) {
                // Still waiting: Explorer's next TaskbarCreated adds the icon.
                log::error!("Could not schedule the retry; waiting for Explorer to restart.");
            }
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
        let mut showing = None;
        if message == NIM_DELETE {
            self.shown = None;
            self.image = None;
        } else if message == NIM_SETVERSION {
            data.Anonymous.uVersion = NOTIFYICON_VERSION_4;
        } else if (message == NIM_ADD || message == NIM_MODIFY)
            && let Some((kind, tooltip)) = &self.current
        {
            let kind = *kind;
            data.uFlags = NIF_TIP | NIF_SHOWTIP;
            copy_truncated(tooltip, &mut data.szTip);
            if message == NIM_ADD || self.shown != Some(kind) {
                data.uFlags |= NIF_MESSAGE;
                data.uCallbackMessage = WM_TRAY;
                // Without an icon, the tooltip and menu still work, so leave the image out
                // rather than hand Explorer a null one.
                if let Some(icon) = self.icon(kind) {
                    data.uFlags |= NIF_ICON;
                    data.hIcon = icon;
                }
            }
            showing = Some(kind);
        }
        let ok = self.shell.notify(message, &data);
        if showing.is_some() {
            // After a failure, the next update sends the icon again.
            self.shown = showing.filter(|_| ok);
            if ok && data.uFlags & NIF_ICON != 0 {
                self.image = showing;
            } else if ok && message == NIM_ADD {
                self.image = None;
            }
        }
        ok
    }

    fn icon(&mut self, kind: IconKind) -> Option<HICON> {
        let slot = &mut self.icons[kind.index()];
        if *slot == IconSlot::Unloaded {
            *slot = match self.shell.load_icon(kind) {
                Ok(icon) => IconSlot::Loaded(icon),
                Err(hr) => {
                    log::error!("Could not load icon {}: {hr:#x}", kind.resource_id());
                    IconSlot::Failed
                }
            };
        }
        match *slot {
            IconSlot::Loaded(icon) => Some(icon),
            IconSlot::Unloaded | IconSlot::Failed => None,
        }
    }

    /// Frees the loaded icons and forgets failures, so the next update loads them afresh.
    fn release_icons(&mut self) {
        for slot in &mut self.icons {
            if let IconSlot::Loaded(icon) = *slot {
                self.shell.destroy_icon(icon);
            }
            *slot = IconSlot::Unloaded;
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

impl<S: Shell> Drop for Tray<S> {
    fn drop(&mut self) {
        self.remove();
        self.release_icons();
    }
}

#[cfg(test)]
mod tests {
    use std::cell::RefCell;
    use std::collections::VecDeque;
    use std::rc::Rc;

    use super::*;

    /// One `Shell_NotifyIconW` call: the message, its flags and whether it carried an icon.
    #[derive(Clone, Copy, Debug, PartialEq, Eq)]
    struct Call(u32, u32, bool);

    #[derive(Debug, Default)]
    struct Record {
        calls: Vec<Call>,
        /// Results for the next notify calls; when empty, calls succeed.
        results: VecDeque<bool>,
        icons_fail: bool,
        loads: u32,
        destroyed: u32,
        timers: Vec<u32>,
        kills: u32,
        /// The image Explorer would display, as a modify without `NIF_ICON` keeps it.
        image: Option<HICON>,
    }

    #[derive(Debug, Default)]
    struct FakeShell(Rc<RefCell<Record>>);

    impl Shell for FakeShell {
        fn notify(&mut self, message: u32, data: &NOTIFYICONDATAW) -> bool {
            let mut record = self.0.borrow_mut();
            record
                .calls
                .push(Call(message, data.uFlags, !data.hIcon.is_null()));
            let ok = record.results.pop_front().unwrap_or(true);
            if message == NIM_DELETE || (ok && message == NIM_ADD) {
                record.image = None;
            }
            if ok && data.uFlags & NIF_ICON != 0 {
                record.image = Some(data.hIcon);
            }
            ok
        }

        fn load_icon(&mut self, kind: IconKind) -> Result<HICON, i32> {
            let mut record = self.0.borrow_mut();
            record.loads += 1;
            if record.icons_fail {
                Err(0x8007_0008_u32 as i32)
            } else {
                Ok(usize::from(kind.resource_id()) as HICON)
            }
        }

        fn destroy_icon(&mut self, _icon: HICON) {
            self.0.borrow_mut().destroyed += 1;
        }

        fn set_retry_timer(&mut self, delay_ms: u32) -> bool {
            self.0.borrow_mut().timers.push(delay_ms);
            true
        }

        fn kill_retry_timer(&mut self) {
            self.0.borrow_mut().kills += 1;
        }
    }

    fn tray() -> (Tray<FakeShell>, Rc<RefCell<Record>>) {
        let record = Rc::new(RefCell::new(Record::default()));
        let tray = Tray::with_shell(ptr::null_mut(), FakeShell(record.clone()));
        (tray, record)
    }

    fn take_calls(record: &Rc<RefCell<Record>>) -> Vec<Call> {
        mem::take(&mut record.borrow_mut().calls)
    }

    const TIP: u32 = NIF_TIP | NIF_SHOWTIP;
    const FULL: u32 = TIP | NIF_MESSAGE | NIF_ICON;

    #[test]
    fn retry_delays_double_up_to_two_seconds_then_stop() {
        let delays: Vec<_> = (0..=MAX_RETRIES).map(retry_delay).collect();
        assert_eq!(&delays[..4], [Some(500), Some(1000), Some(2000), Some(2000)]);
        assert_eq!(delays[MAX_RETRIES as usize - 1], Some(2000));
        assert_eq!(delays[MAX_RETRIES as usize], None);
        assert_eq!(retry_delay(u32::MAX), None);
    }

    #[test]
    fn countdown_updates_send_only_the_tooltip() {
        let (mut tray, record) = tray();
        tray.show(IconKind::Timed, "1:00");
        assert_eq!(
            take_calls(&record),
            [Call(NIM_ADD, FULL, true), Call(NIM_SETVERSION, 0, false)]
        );
        tray.show(IconKind::Timed, "1:00");
        assert!(take_calls(&record).is_empty(), "an unchanged icon is not resent");
        tray.show(IconKind::Timed, "0:59");
        assert_eq!(take_calls(&record), [Call(NIM_MODIFY, TIP, false)]);
        tray.show(IconKind::Passive, "Off");
        assert_eq!(take_calls(&record), [Call(NIM_MODIFY, FULL, true)]);
        assert_eq!(record.borrow().loads, 2, "each icon is loaded once");
    }

    #[test]
    fn failed_adds_back_off_then_recover_on_retry() {
        let (mut tray, record) = tray();
        // NIM_ADD and the NIM_MODIFY fallback both fail, twice.
        record.borrow_mut().results.extend([false; 4]);
        tray.show(IconKind::Indefinite, "On");
        assert!(tray.waiting && !tray.added);
        assert_eq!(record.borrow().timers, [500]);

        // While a retry is pending, changes are only recorded.
        take_calls(&record);
        tray.show(IconKind::Indefinite, "Still on");
        assert!(take_calls(&record).is_empty());

        tray.retry();
        assert_eq!(record.borrow().timers, [500, 1000]);
        assert_eq!(tray.retries, 2);

        take_calls(&record);
        tray.retry();
        assert!(tray.added && !tray.waiting);
        assert_eq!(tray.retries, 0);
        assert_eq!(
            take_calls(&record),
            [Call(NIM_ADD, FULL, true), Call(NIM_SETVERSION, 0, false)]
        );
        assert_eq!(record.borrow().kills, 2);
    }

    #[test]
    fn an_add_rejected_because_the_icon_exists_falls_back_to_modify() {
        let (mut tray, record) = tray();
        record.borrow_mut().results.push_back(false);
        tray.show(IconKind::Indefinite, "On");
        assert!(tray.added);
        assert_eq!(
            take_calls(&record),
            [
                Call(NIM_ADD, FULL, true),
                Call(NIM_MODIFY, FULL, true),
                Call(NIM_SETVERSION, 0, false)
            ]
        );
    }

    #[test]
    fn exhausted_retries_wait_for_explorer_to_restart() {
        let (mut tray, record) = tray();
        record
            .borrow_mut()
            .results
            .extend([false; 2 * (MAX_RETRIES as usize + 1)]);
        tray.show(IconKind::Indefinite, "On");
        for _ in 0..MAX_RETRIES {
            tray.retry();
        }
        assert!(tray.waiting && !tray.added);
        assert_eq!(record.borrow().timers.len(), MAX_RETRIES as usize);
        assert!(record.borrow().results.is_empty());

        take_calls(&record);
        tray.show(IconKind::Timed, "1:00");
        assert!(
            take_calls(&record).is_empty(),
            "no more attempts until Explorer returns"
        );

        tray.taskbar_created();
        assert!(tray.added && !tray.waiting);
        assert_eq!(
            take_calls(&record),
            [
                Call(NIM_DELETE, 0, false),
                Call(NIM_ADD, FULL, true),
                Call(NIM_SETVERSION, 0, false)
            ]
        );
    }

    #[test]
    fn a_failed_modify_resends_the_icon_next_time() {
        let (mut tray, record) = tray();
        tray.show(IconKind::Timed, "1:00");
        take_calls(&record);
        // The update fails, and so does the re-add; the fallback modify succeeds.
        record.borrow_mut().results.extend([false, false]);
        tray.show(IconKind::Timed, "0:59");
        assert_eq!(
            take_calls(&record),
            [
                Call(NIM_MODIFY, TIP, false),
                Call(NIM_ADD, FULL, true),
                Call(NIM_MODIFY, FULL, true),
                Call(NIM_SETVERSION, 0, false)
            ]
        );
    }

    #[test]
    fn an_icon_that_fails_to_load_is_not_retried_every_update() {
        let (mut tray, record) = tray();
        record.borrow_mut().icons_fail = true;
        tray.show(IconKind::Timed, "1:00");
        assert_eq!(
            take_calls(&record),
            [
                Call(NIM_ADD, TIP | NIF_MESSAGE, false),
                Call(NIM_SETVERSION, 0, false)
            ],
            "the icon is added without an image rather than with a null one"
        );
        for second in 0..5 {
            tray.show(IconKind::Timed, &format!("0:5{second}"));
        }
        assert_eq!(record.borrow().loads, 1);

        // A display change is a fresh start: the load is tried again.
        record.borrow_mut().icons_fail = false;
        tray.display_changed();
        assert_eq!(record.borrow().loads, 2);
        assert_eq!(take_calls(&record).last(), Some(&Call(NIM_MODIFY, FULL, true)));
    }

    #[test]
    fn a_state_change_whose_icon_fails_does_not_keep_the_old_image() {
        let (mut tray, record) = tray();
        tray.show(IconKind::Indefinite, "On");
        assert!(record.borrow().image.is_some());
        take_calls(&record);

        record.borrow_mut().icons_fail = true;
        tray.show(IconKind::Passive, "Off");
        assert_eq!(
            take_calls(&record),
            [
                Call(NIM_DELETE, 0, false),
                Call(NIM_ADD, TIP | NIF_MESSAGE, false),
                Call(NIM_SETVERSION, 0, false)
            ],
            "the entry is re-created without an image"
        );
        assert_eq!(record.borrow().image, None, "the kept-awake image is gone");
        assert!(tray.added);

        // Further updates of the same state change only the tooltip.
        tray.show(IconKind::Passive, "Still off");
        assert_eq!(take_calls(&record), [Call(NIM_MODIFY, TIP, false)]);

        // Back to a state whose icon loaded: it is shown again in place.
        tray.show(IconKind::Indefinite, "On");
        assert_eq!(take_calls(&record), [Call(NIM_MODIFY, FULL, true)]);
        assert!(record.borrow().image.is_some());
    }

    #[test]
    fn only_a_missing_icon_for_another_state_needs_a_fresh_entry() {
        use IconKind::{Indefinite, Passive};
        assert!(leaves_stale_image(Some(Indefinite), Passive, false));
        assert!(!leaves_stale_image(Some(Indefinite), Passive, true));
        assert!(!leaves_stale_image(Some(Passive), Passive, false));
        assert!(!leaves_stale_image(None, Passive, false));
    }

    #[test]
    fn display_changes_reload_icons_and_drop_cleans_up() {
        let (mut tray, record) = tray();
        tray.show(IconKind::Timed, "1:00");
        tray.display_changed();
        assert_eq!(record.borrow().destroyed, 1);
        assert_eq!(record.borrow().loads, 2);
        take_calls(&record);
        drop(tray);
        assert_eq!(take_calls(&record), [Call(NIM_DELETE, 0, false)]);
        assert_eq!(record.borrow().destroyed, 2);
    }

    #[test]
    fn nothing_is_sent_before_there_is_something_to_show() {
        let (mut tray, record) = tray();
        tray.retry();
        tray.return_focus();
        tray.display_changed();
        assert!(take_calls(&record).is_empty());
    }
}
