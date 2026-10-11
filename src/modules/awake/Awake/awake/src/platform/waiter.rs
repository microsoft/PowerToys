//! A background thread that waits on kernel objects and forwards what happened to the window
//! thread as posted messages. It never touches application state.
//!
//! It also guards against a stuck UI thread: once a stop has been requested (exit event, the
//! bound process ending, or the deadline of a session that ends the process) the process must be
//! gone within [`watchdog`], or this thread ends it. Ctrl+C has the same backstop in
//! `console.rs`. Windows releases the keep-awake request when the process dies, so the machine is
//! never held awake by a hung Awake.
//!
//! Only this thread arms or cancels the deadline timer. The window thread records the deadline it
//! wants and signals a change; `SetWaitableTimer` replaces rather than compares, so two writers
//! could leave an older deadline armed over a newer one.

use std::ffi::OsString;
use std::os::windows::ffi::OsStrExt;
use std::os::windows::io::{AsRawHandle, FromRawHandle, OwnedHandle};
use std::path::Path;
use std::sync::Arc;
use std::sync::atomic::{AtomicBool, AtomicI64, Ordering};
use std::sync::mpsc::{self, SyncSender};
use std::thread::JoinHandle;
use std::time::{Duration, Instant};
use std::{io, mem, ptr, slice};

use awake_core::time::Timestamp;
use windows_sys::Win32::Foundation::{HANDLE, HWND, INVALID_HANDLE_VALUE, WAIT_OBJECT_0, WAIT_TIMEOUT};
use windows_sys::Win32::Storage::FileSystem::{
    CreateFileW, FILE_FLAG_BACKUP_SEMANTICS, FILE_FLAG_OVERLAPPED, FILE_LIST_DIRECTORY,
    FILE_NOTIFY_CHANGE_CREATION, FILE_NOTIFY_CHANGE_FILE_NAME, FILE_NOTIFY_CHANGE_LAST_WRITE,
    FILE_NOTIFY_CHANGE_SIZE, FILE_NOTIFY_INFORMATION, FILE_SHARE_DELETE, FILE_SHARE_READ, FILE_SHARE_WRITE,
    OPEN_EXISTING, ReadDirectoryChangesW,
};
use windows_sys::Win32::System::IO::{CancelIoEx, GetOverlappedResult, OVERLAPPED};
use windows_sys::Win32::System::Threading::{
    CancelWaitableTimer, CreateWaitableTimerExW, GetCurrentProcess, INFINITE, ResetEvent, SetEvent,
    SetWaitableTimer, TIMER_ALL_ACCESS, TerminateProcess, WaitForMultipleObjects, WaitForSingleObject,
};
use windows_sys::Win32::UI::WindowsAndMessaging::PostMessageW;

use super::clock::SystemClock;
use super::messages::WM_WAITER;
use super::{last_error, wide::wide};

const WATCHDOG: Duration = Duration::from_secs(10);

/// How long a stop request may take before the process is ended.
pub fn watchdog() -> Duration {
    crate::hooks::duration("POWERTOYS_AWAKE_TEST_WATCHDOG_MS", WATCHDOG)
}
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
                handles.push(w.event.as_raw_handle());
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
        let limit = Instant::now() + watchdog();
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
                _ => end_hung_process(),
            }
        }
    }

    fn watchdog(&self) {
        let millis = watchdog().as_millis() as u32;
        // SAFETY: valid event handle.
        if unsafe { WaitForSingleObject(self.stop.as_raw_handle(), millis) } == WAIT_TIMEOUT {
            end_hung_process();
        }
    }
}

/// Ends the process because the window thread did not act on a stop request in time. Windows
/// releases every keep-awake request the process held.
pub fn end_hung_process() -> ! {
    log::error!(
        "The window thread did not shut down within {:?}; ending the process.",
        watchdog()
    );
    // TerminateProcess skips every normal exit path, so hand the shell back its console mode
    // here; it is one SetConsoleMode call that takes no locks.
    super::console::restore_mode();
    crate::logging::flush(Duration::from_millis(500));
    // SAFETY: terminating our own process.
    unsafe { TerminateProcess(GetCurrentProcess(), 1) };
    std::process::abort()
}

fn arm_timer(timer: HANDLE, ticks: i64) {
    // A positive due time is absolute (UTC FILETIME); zero would fire immediately.
    let due = ticks.max(1);
    // SAFETY: valid timer handle and due-time pointer; no completion routine.
    if unsafe { SetWaitableTimer(timer, &due, 0, None, ptr::null(), 0) } == 0 {
        log::error!("Could not arm the deadline timer: {}", last_error());
    }
}

/// Bytes of change records one request can return.
const WATCH_BUFFER_BYTES: usize = 8192;

/// The buffer `ReadDirectoryChangesW` fills; records must be DWORD-aligned.
type WatchBuffer = [u32; WATCH_BUFFER_BYTES / 4];

/// Overlapped `ReadDirectoryChangesW` on one directory, filtered to one file name.
///
/// The OVERLAPPED and the buffer are written by the kernel while a request is pending, so they
/// are held as raw pointers from `Box::into_raw` rather than as `Box`es, which would claim unique
/// access to memory something else is writing. They are freed in `Drop` only once the request
/// has been cancelled and has completed. The raw pointers also keep the type `!Send`: it is
/// created, serviced and dropped on the waiter thread, the thread that issued the request.
struct DirectoryWatch {
    directory: OwnedHandle,
    event: OwnedHandle,
    file_name: Vec<u16>,
    overlapped: *mut OVERLAPPED,
    buffer: *mut WatchBuffer,
    armed: bool,
}

impl DirectoryWatch {
    fn start(directory: &Path, file_name: &OsString) -> io::Result<Self> {
        let path = wide(directory);
        // SAFETY: `path` is NUL-terminated; directory handles need FILE_FLAG_BACKUP_SEMANTICS.
        let raw = unsafe {
            CreateFileW(
                path.as_ptr(),
                FILE_LIST_DIRECTORY,
                FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
                ptr::null(),
                OPEN_EXISTING,
                FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OVERLAPPED,
                ptr::null_mut(),
            )
        };
        if raw == INVALID_HANDLE_VALUE {
            return Err(last_error());
        }
        let mut watch = DirectoryWatch {
            // SAFETY: fresh handle we own.
            directory: unsafe { OwnedHandle::from_raw_handle(raw) },
            event: super::event::manual_reset(None)?,
            file_name: file_name.encode_wide().map(lower).collect(),
            // SAFETY: OVERLAPPED is plain data, for which all zeroes is a valid value.
            overlapped: Box::into_raw(Box::new(unsafe { mem::zeroed() })),
            buffer: Box::into_raw(Box::new([0; WATCH_BUFFER_BYTES / 4])),
            armed: false,
        };
        watch.arm()?;
        Ok(watch)
    }

    fn arm(&mut self) -> io::Result<()> {
        debug_assert!(!self.armed, "a request is already pending");
        // SAFETY: valid event handle.
        unsafe { ResetEvent(self.event.as_raw_handle()) };
        // SAFETY: no request is pending, so nothing else uses the OVERLAPPED; the pointer came
        // from `Box::into_raw` and stays valid until `Drop`.
        unsafe {
            self.overlapped.write(OVERLAPPED {
                hEvent: self.event.as_raw_handle(),
                // SAFETY: OVERLAPPED is plain data, for which all zeroes is a valid value.
                ..mem::zeroed()
            });
        }
        let filter = FILE_NOTIFY_CHANGE_FILE_NAME
            | FILE_NOTIFY_CHANGE_LAST_WRITE
            | FILE_NOTIFY_CHANGE_SIZE
            | FILE_NOTIFY_CHANGE_CREATION;
        // SAFETY: the buffer and OVERLAPPED are live heap allocations that are only reached
        // through these raw pointers while the request is pending; `Drop` cancels the request
        // and waits for it to complete before freeing them.
        let ok = unsafe {
            ReadDirectoryChangesW(
                self.directory.as_raw_handle(),
                self.buffer.cast(),
                WATCH_BUFFER_BYTES as u32,
                0,
                filter,
                ptr::null_mut(),
                self.overlapped,
                None,
            )
        } != 0;
        self.armed = ok;
        if ok { Ok(()) } else { Err(last_error()) }
    }

    /// Whether the completed batch mentions the watched file. An overflowed batch counts as a
    /// change because its details were lost.
    fn take_relevant_change(&mut self) -> bool {
        let mut bytes = 0u32;
        // SAFETY: the request completed (its event is signaled), so no wait is needed and the
        // kernel no longer writes to the OVERLAPPED.
        let ok =
            unsafe { GetOverlappedResult(self.directory.as_raw_handle(), self.overlapped, &mut bytes, 0) }
                != 0;
        self.armed = false;
        if !ok || bytes == 0 {
            return true;
        }
        let len = (bytes as usize).min(WATCH_BUFFER_BYTES);
        // SAFETY: the request completed, so the kernel is done with the buffer until the next
        // `arm`, which needs `&mut self` and so cannot overlap this borrow. It holds at least
        // `len` initialized bytes.
        let records = unsafe { slice::from_raw_parts(self.buffer.cast::<u8>(), len) };
        batch_mentions(records, &self.file_name)
    }
}

impl Drop for DirectoryWatch {
    fn drop(&mut self) {
        if self.armed {
            let mut bytes = 0;
            // SAFETY: cancel the pending request and wait (bWait = TRUE) until it completes, after
            // which the kernel no longer touches the OVERLAPPED or the buffer.
            unsafe {
                CancelIoEx(self.directory.as_raw_handle(), self.overlapped);
                GetOverlappedResult(self.directory.as_raw_handle(), self.overlapped, &mut bytes, 1);
            }
        }
        // SAFETY: both came from `Box::into_raw` in `start`, are freed only here, and no request
        // is pending any more.
        unsafe {
            drop(Box::from_raw(self.overlapped));
            drop(Box::from_raw(self.buffer));
        }
    }
}

/// Whether a batch of `FILE_NOTIFY_INFORMATION` records names `file_name` (already lowercase).
/// Reads the fields byte by byte, so a malformed batch can end the walk early but never cause an
/// out-of-bounds or misaligned read.
fn batch_mentions(records: &[u8], file_name: &[u16]) -> bool {
    const NEXT: usize = mem::offset_of!(FILE_NOTIFY_INFORMATION, NextEntryOffset);
    const NAME_LENGTH: usize = mem::offset_of!(FILE_NOTIFY_INFORMATION, FileNameLength);
    const NAME: usize = mem::offset_of!(FILE_NOTIFY_INFORMATION, FileName);
    let read_u32 = |at: usize| -> Option<u32> {
        let bytes = records.get(at..at.checked_add(4)?)?;
        Some(u32::from_ne_bytes(bytes.try_into().ok()?))
    };
    let mut offset = 0usize;
    loop {
        debug_assert_eq!(
            offset % mem::align_of::<FILE_NOTIFY_INFORMATION>(),
            0,
            "the kernel aligns every record to a DWORD"
        );
        let (Some(next), Some(name_bytes)) = (read_u32(offset + NEXT), read_u32(offset + NAME_LENGTH)) else {
            return false;
        };
        let start = offset + NAME;
        let Some(name) = start
            .checked_add(name_bytes as usize)
            .and_then(|end| records.get(start..end))
        else {
            return false;
        };
        let units = name
            .chunks_exact(2)
            .map(|pair| u16::from_ne_bytes([pair[0], pair[1]]));
        if name.len() == file_name.len() * 2 && units.map(lower).eq(file_name.iter().copied()) {
            return true;
        }
        if next == 0 {
            return false;
        }
        offset += next as usize;
    }
}

fn lower(unit: u16) -> u16 {
    if (u16::from(b'A')..=u16::from(b'Z')).contains(&unit) {
        unit + 32
    } else {
        unit
    }
}

#[cfg(test)]
mod tests {
    use windows_sys::Win32::UI::WindowsAndMessaging::{
        CreateWindowExW, DestroyWindow, HWND_MESSAGE, MSG, PM_REMOVE, PeekMessageW,
    };

    use super::*;

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

    /// One `FILE_NOTIFY_INFORMATION` record, padded to a DWORD as the kernel lays them out.
    fn record(name: &str, last: bool) -> Vec<u8> {
        let name: Vec<u8> = name.encode_utf16().flat_map(u16::to_ne_bytes).collect();
        let size = (12 + name.len()).next_multiple_of(4);
        let mut bytes = Vec::with_capacity(size);
        bytes.extend_from_slice(&(if last { 0 } else { size as u32 }).to_ne_bytes());
        bytes.extend_from_slice(&3u32.to_ne_bytes()); // FILE_ACTION_MODIFIED
        bytes.extend_from_slice(&(name.len() as u32).to_ne_bytes());
        bytes.extend_from_slice(&name);
        bytes.resize(size, 0);
        bytes
    }

    fn lowered(name: &str) -> Vec<u16> {
        name.encode_utf16().map(lower).collect()
    }

    #[test]
    fn finds_the_watched_file_in_any_record_ignoring_ascii_case() {
        let target = lowered("settings.json");
        let mut batch = record("settings.json.tmp", false);
        batch.extend(record("Settings.JSON", true));
        assert!(batch_mentions(&batch, &target));
        assert!(!batch_mentions(&record("other.json", true), &target));
        assert!(!batch_mentions(&record("settings.jso", true), &target));
    }

    #[test]
    fn malformed_batches_end_the_walk_without_reading_out_of_bounds() {
        let target = lowered("settings.json");
        let full = record("settings.json", true);
        // Cut off inside the header and inside the name.
        assert!(!batch_mentions(&full[..8], &target));
        assert!(!batch_mentions(&full[..full.len() - 4], &target));
        // A name length running far past the end.
        let mut huge = record("a", true);
        huge[8..12].copy_from_slice(&u32::MAX.to_ne_bytes());
        assert!(!batch_mentions(&huge, &target));
        // A next offset that points past the end.
        let mut dangling = record("a", false);
        dangling[..4].copy_from_slice(&4096u32.to_ne_bytes());
        assert!(!batch_mentions(&dangling, &target));
        assert!(!batch_mentions(&[], &target));
    }
}
