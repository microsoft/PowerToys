//! A background thread that waits on kernel objects and forwards what happened to the window
//! thread as posted messages. It never touches application state.
//!
//! It also guards against a stuck UI thread: once a stop has been requested (exit event, the
//! bound process ending, or the deadline of a session that ends the process) it gives the window
//! thread [`watchdog::limit`] to exit, then ends the process.
//!
//! Only this thread arms or cancels the deadline timer. The window thread records the deadline it
//! wants and signals a change; `SetWaitableTimer` replaces rather than compares, so two writers
//! could leave an older deadline armed over a newer one.

use std::ffi::OsString;
use std::os::windows::io::{AsRawHandle, FromRawHandle, OwnedHandle};
use std::sync::Arc;
use std::sync::atomic::{AtomicBool, AtomicI64, Ordering};
use std::sync::mpsc::{self, SyncSender};
use std::thread::JoinHandle;
use std::time::{Duration, Instant};
use std::{io, ptr};

use awake_core::time::Timestamp;
use windows_sys::Win32::Foundation::{HANDLE, HWND, WAIT_OBJECT_0, WAIT_TIMEOUT};
use windows_sys::Win32::System::Threading::{
    CancelWaitableTimer, CreateWaitableTimerExW, INFINITE, ResetEvent, SetEvent, SetWaitableTimer,
    TIMER_ALL_ACCESS, WaitForMultipleObjects, WaitForSingleObject,
};
use windows_sys::Win32::UI::WindowsAndMessaging::PostMessageW;

use super::clock::SystemClock;
use super::last_error;
use super::messages::WM_WAITER;
use super::watch::DirectoryWatch;
use super::watchdog;

/// How often to try watching the settings folder again after it disappeared.
const WATCH_RETRY: Duration = Duration::from_secs(2);

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
#[repr(usize)]
pub enum WaitEvent {
    ExitRequested = 1,
    ProcessExited = 2,
    SettingsChanged = 3,
    DeadlineReached = 4,
}

impl WaitEvent {
    pub fn from_wparam(value: usize) -> Option<WaitEvent> {
        [
            WaitEvent::ExitRequested,
            WaitEvent::ProcessExited,
            WaitEvent::SettingsChanged,
            WaitEvent::DeadlineReached,
        ]
        .into_iter()
        .find(|event| *event as usize == value)
    }
}

pub struct WaiterSetup {
    pub window: HWND,
    pub exit_event: OwnedHandle,
    pub process: Option<OwnedHandle>,
    /// Directory and file name to watch for changes.
    pub watch: Option<(std::path::PathBuf, OsString)>,
    /// The deadline ends the process (command-line sessions), so it gets the watchdog too.
    pub deadline_ends_process: bool,
}

#[derive(Debug)]
pub struct Waiter {
    stop: OwnedHandle,
    /// The deadline the window thread wants, in ticks, `NO_DEADLINE` for none.
    deadline: Arc<AtomicI64>,
    /// Tells the waiter thread that `deadline` changed.
    deadline_changed: OwnedHandle,
    settings_pending: Arc<AtomicBool>,
    thread: Option<JoinHandle<()>>,
}

const NO_DEADLINE: i64 = i64::MIN;

impl Waiter {
    pub fn start(setup: WaiterSetup) -> io::Result<Waiter> {
        let stop = super::event::manual_reset(None)?;
        // SAFETY: anonymous synchronization timer; null attributes and name are allowed.
        let timer = unsafe { CreateWaitableTimerExW(ptr::null(), ptr::null(), 0, TIMER_ALL_ACCESS) };
        if timer.is_null() {
            return Err(last_error());
        }
        // SAFETY: fresh handle we own.
        let timer = unsafe { OwnedHandle::from_raw_handle(timer) };
        let settings_pending = Arc::new(AtomicBool::new(false));
        let deadline = Arc::new(AtomicI64::new(NO_DEADLINE));
        let deadline_changed = super::event::manual_reset(None)?;

        let context = Context {
            window: setup.window as usize,
            stop: stop.try_clone()?,
            exit_event: setup.exit_event,
            timer,
            deadline: deadline.clone(),
            deadline_changed: deadline_changed.try_clone()?,
            deadline_ends_process: setup.deadline_ends_process,
            process: setup.process,
            watch_target: setup.watch,
            settings_pending: settings_pending.clone(),
        };
        // The watch is created on the thread that services it: Windows cancels overlapped I/O
        // when the issuing thread exits, and keeping it on one thread lets it stay `!Send`.
        let (ready, armed) = mpsc::sync_channel(1);
        let thread = std::thread::Builder::new()
            .name("awake-waiter".into())
            .spawn(move || context.run(ready))?;
        // The caller reads the settings next, so a change made after that read must already be
        // seen. An error means the thread ended early; the join in `shutdown` reports nothing
        // more useful, so carry on as with a failed watch.
        let _ = armed.recv();
        Ok(Waiter {
            stop,
            deadline,
            deadline_changed,
            settings_pending,
            thread: Some(thread),
        })
    }

    /// Has the waiter thread arm the one-shot deadline at an absolute time, which Windows keeps
    /// correct across clock changes and sleep. `None` cancels it.
    pub fn set_deadline(&self, deadline: Option<Timestamp>) {
        let ticks = deadline.map_or(NO_DEADLINE, Timestamp::ticks);
        self.deadline.store(ticks, Ordering::Release);
        // SAFETY: valid event handle.
        unsafe { SetEvent(self.deadline_changed.as_raw_handle()) };
    }

    /// Lets the next settings change be posted again; call before reading the file.
    pub fn settings_handled(&self) {
        self.settings_pending.store(false, Ordering::Release);
    }

    pub fn stop(mut self) {
        self.shutdown();
    }

    fn shutdown(&mut self) {
        // SAFETY: valid event handle.
        unsafe { SetEvent(self.stop.as_raw_handle()) };
        if let Some(thread) = self.thread.take() {
            let _ = thread.join();
        }
    }
}

impl Drop for Waiter {
    fn drop(&mut self) {
        self.shutdown();
    }
}

struct Context {
    window: usize,
    stop: OwnedHandle,
    exit_event: OwnedHandle,
    timer: OwnedHandle,
    deadline: Arc<AtomicI64>,
    deadline_changed: OwnedHandle,
    deadline_ends_process: bool,
    process: Option<OwnedHandle>,
    /// What to watch, kept so the watch can be restored if the folder is deleted and recreated.
    watch_target: Option<(std::path::PathBuf, OsString)>,
    settings_pending: Arc<AtomicBool>,
}

impl Context {
    /// Runs on the waiter thread; `ready` is signaled once the first watch request is issued.
    fn run(mut self, ready: SyncSender<()>) {
        let mut watch =
            self.watch_target
                .as_ref()
                .and_then(|(dir, file)| match DirectoryWatch::start(dir, file) {
                    Ok(watch) => Some(watch),
                    Err(error) => {
                        log::warn!("Not watching {} for changes yet: {error}", dir.display());
                        None
                    }
                });
        let _ = ready.send(());
        drop(ready);
        loop {
            let mut handles: Vec<HANDLE> = vec![
                self.stop.as_raw_handle(),
                self.exit_event.as_raw_handle(),
                self.timer.as_raw_handle(),
                self.deadline_changed.as_raw_handle(),
            ];
            let process_index = self.process.as_ref().map(|p| {
                handles.push(p.as_raw_handle());
                handles.len() - 1
            });
            let watch_index = watch.as_ref().map(|w| {
                handles.push(w.event());
                handles.len() - 1
            });

            // Without a working watch, wake up now and then to try again.
            let timeout = if watch.is_none() && self.watch_target.is_some() {
                WATCH_RETRY.as_millis() as u32
            } else {
                INFINITE
            };
            // SAFETY: every handle in the list is owned by this context and stays open.
            let result =
                unsafe { WaitForMultipleObjects(handles.len() as u32, handles.as_ptr(), 0, timeout) };
            if result == WAIT_TIMEOUT {
                watch = self.restore_watch();
                continue;
            }
            let Some(index) = result
                .checked_sub(WAIT_OBJECT_0)
                .map(|i| i as usize)
                .filter(|i| *i < handles.len())
            else {
                // Nothing would observe the exit event, the bound process or the deadline any
                // more, so stop rather than hold the machine awake unattended.
                log::error!("Waiting failed ({result:#x}): {}", last_error());
                self.post(WaitEvent::ExitRequested);
                self.watchdog();
                return;
            };

            match index {
                0 => return,
                1 => {
                    self.post(WaitEvent::ExitRequested);
                    self.watchdog();
                    return;
                }
                2 => {
                    // Early (see `deadline_is_due`), or fired just before a cancel or a later
                    // deadline reached this thread.
                    if !self.deadline_is_due() {
                        self.sync_timer();
                        continue;
                    }
                    self.post(WaitEvent::DeadlineReached);
                    if self.deadline_ends_process && !self.deadline_watchdog() {
                        return;
                    }
                }
                3 => {
                    // SAFETY: valid event handle. Reset before reading, so a change stored after
                    // the read signals again.
                    unsafe { ResetEvent(self.deadline_changed.as_raw_handle()) };
                    self.sync_timer();
                }
                i if Some(i) == process_index => {
                    self.process = None;
                    self.post(WaitEvent::ProcessExited);
                    self.watchdog();
                    return;
                }
                i if Some(i) == watch_index => {
                    let active = watch.as_mut().expect("index came from the watch");
                    let relevant = active.take_relevant_change();
                    let rearmed = active.arm();
                    if relevant && !self.settings_pending.swap(true, Ordering::AcqRel) {
                        self.post(WaitEvent::SettingsChanged);
                    }
                    if let Err(error) = rearmed {
                        log::warn!("Lost the settings watch, will retry: {error}");
                        watch = None;
                    }
                }
                _ => {}
            }
        }
    }

    fn post(&self, event: WaitEvent) {
        // SAFETY: posting to a window owned by another thread is allowed; a destroyed window
        // just makes the call fail.
        let posted = unsafe { PostMessageW(self.window as HWND, WM_WAITER, event as usize, 0) } != 0;
        if !posted && event == WaitEvent::SettingsChanged {
            // Nobody will clear the flag, so clear it here or later changes would be ignored.
            self.settings_pending.store(false, Ordering::Release);
        }
    }

    /// Tries to watch the settings folder again after it disappeared.
    fn restore_watch(&self) -> Option<DirectoryWatch> {
        let (dir, file) = self.watch_target.as_ref()?;
        let watch = DirectoryWatch::start(dir, file).ok()?;
        log::info!("Watching {} again.", dir.display());
        // Changes made while unwatched would otherwise go unnoticed.
        if !self.settings_pending.swap(true, Ordering::AcqRel) {
            self.post(WaitEvent::SettingsChanged);
        }
        Some(watch)
    }

    /// Whether the deadline has come. The timer runs on interrupt time, which clock corrections
    /// do not move, so it can fire while the system clock still reads a little before the
    /// deadline.
    fn deadline_is_due(&self) -> bool {
        let deadline = self.deadline.load(Ordering::Acquire);
        deadline != NO_DEADLINE && SystemClock.now_utc().ticks() >= deadline
    }

    /// Arms the timer for the deadline the window thread last asked for, or cancels it.
    fn sync_timer(&self) {
        let timer = self.timer.as_raw_handle();
        match self.deadline.load(Ordering::Acquire) {
            NO_DEADLINE => {
                // SAFETY: valid timer handle.
                unsafe { CancelWaitableTimer(timer) };
            }
            ticks => arm_timer(timer, ticks),
        }
    }

    /// The watchdog after a deadline that ends the process. Returns true when the window thread
    /// sets a deadline again instead of exiting, as it does when the system clock it reads is
    /// still short of the deadline: the session goes on, and so does the main wait. A cancelled
    /// deadline is the session ending, so the watchdog keeps counting then.
    fn deadline_watchdog(&self) -> bool {
        let limit = Instant::now() + watchdog::limit();
        let handles = [self.stop.as_raw_handle(), self.deadline_changed.as_raw_handle()];
        loop {
            let left = limit.saturating_duration_since(Instant::now()).as_millis() as u32;
            // SAFETY: both handles are owned by this context and stay open.
            let result = unsafe { WaitForMultipleObjects(2, handles.as_ptr(), 0, left) };
            match result {
                r if r == WAIT_OBJECT_0 => return false,
                r if r == WAIT_OBJECT_0 + 1 => {
                    if self.deadline.load(Ordering::Acquire) != NO_DEADLINE {
                        // Left signaled, so the main loop arms the timer.
                        return true;
                    }
                    // SAFETY: valid event handle.
                    unsafe { ResetEvent(self.deadline_changed.as_raw_handle()) };
                    self.sync_timer();
                }
                _ => watchdog::end_hung_process(),
            }
        }
    }

    fn watchdog(&self) {
        let millis = watchdog::limit().as_millis() as u32;
        // SAFETY: valid event handle.
        if unsafe { WaitForSingleObject(self.stop.as_raw_handle(), millis) } == WAIT_TIMEOUT {
            watchdog::end_hung_process();
        }
    }
}

fn arm_timer(timer: HANDLE, ticks: i64) {
    // A positive due time is absolute (UTC FILETIME); zero would fire immediately.
    let due = ticks.max(1);
    // SAFETY: valid timer handle and due-time pointer; no completion routine.
    if unsafe { SetWaitableTimer(timer, &due, 0, None, ptr::null(), 0) } == 0 {
        log::error!("Could not arm the deadline timer: {}", last_error());
    }
}

#[cfg(test)]
mod tests {
    use std::mem;

    use windows_sys::Win32::UI::WindowsAndMessaging::{
        CreateWindowExW, DestroyWindow, HWND_MESSAGE, MSG, PM_REMOVE, PeekMessageW,
    };

    use super::*;
    use crate::platform::wide::wide;

    /// A message-only window on the test thread that collects what the waiter posts.
    struct Inbox(HWND);

    impl Inbox {
        fn new() -> Inbox {
            let class = wide("STATIC");
            // SAFETY: a predefined class needs no registration; the window is destroyed in Drop.
            let window = unsafe {
                CreateWindowExW(
                    0,
                    class.as_ptr(),
                    ptr::null(),
                    0,
                    0,
                    0,
                    0,
                    0,
                    HWND_MESSAGE,
                    ptr::null_mut(),
                    ptr::null_mut(),
                    ptr::null(),
                )
            };
            assert!(!window.is_null(), "{}", last_error());
            Inbox(window)
        }

        fn next(&self, timeout: Duration) -> Option<WaitEvent> {
            let limit = Instant::now() + timeout;
            loop {
                // SAFETY: MSG is plain data.
                let mut message: MSG = unsafe { mem::zeroed() };
                // SAFETY: our own window, polled on the thread that created it.
                if unsafe { PeekMessageW(&mut message, self.0, WM_WAITER, WM_WAITER, PM_REMOVE) } != 0 {
                    return WaitEvent::from_wparam(message.wParam);
                }
                if Instant::now() >= limit {
                    return None;
                }
                std::thread::sleep(Duration::from_millis(5));
            }
        }

        fn waiter(&self, deadline_ends_process: bool) -> Waiter {
            Waiter::start(WaiterSetup {
                window: self.0,
                exit_event: super::super::event::manual_reset(None).unwrap(),
                process: None,
                watch: None,
                deadline_ends_process,
            })
            .unwrap()
        }
    }

    impl Drop for Inbox {
        fn drop(&mut self) {
            // SAFETY: created in `new` on this thread.
            unsafe { DestroyWindow(self.0) };
        }
    }

    fn in_millis(millis: i64) -> Timestamp {
        Timestamp::from_ticks(SystemClock.now_utc().ticks() + millis * 10_000)
    }

    const SOON: Duration = Duration::from_secs(3);

    // Pins the single-writer design: a later deadline replaces an earlier one and a cancel
    // sticks. The cross-thread race that design removes cannot be staged from a test; what
    // guards it is that only the waiter thread ever touches the kernel timer.
    #[test]
    fn a_later_deadline_replaces_an_earlier_one() {
        let inbox = Inbox::new();
        let waiter = inbox.waiter(false);
        waiter.set_deadline(Some(in_millis(60_000)));
        waiter.set_deadline(Some(in_millis(50)));
        assert_eq!(inbox.next(SOON), Some(WaitEvent::DeadlineReached));

        waiter.set_deadline(Some(in_millis(50)));
        waiter.set_deadline(None);
        assert_eq!(
            inbox.next(Duration::from_millis(300)),
            None,
            "a cancelled deadline never fires"
        );
        waiter.stop();
    }

    // A session that ends the process puts the waiter on watch after its deadline. When the
    // window thread finds the clock still short of it and sets the deadline again, the session
    // goes on, so the waiter must go back to waiting rather than end a healthy process.
    #[test]
    fn a_deadline_set_again_after_firing_is_waited_for() {
        let inbox = Inbox::new();
        let waiter = inbox.waiter(true);
        waiter.set_deadline(Some(in_millis(50)));
        assert_eq!(inbox.next(SOON), Some(WaitEvent::DeadlineReached));
        waiter.set_deadline(Some(in_millis(50)));
        assert_eq!(inbox.next(SOON), Some(WaitEvent::DeadlineReached));
        waiter.stop();
    }
}
