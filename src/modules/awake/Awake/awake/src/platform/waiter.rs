//! A background thread that waits on kernel objects and forwards what happened to the window
//! thread as posted messages. It never touches application state.
//!
//! It also guards against a stuck UI thread: once a stop has been requested (exit event, the
//! bound process ending, or the deadline of a session that ends the process) the process must be
//! gone within [`watchdog`], or this thread ends it. Ctrl+C has the same backstop in
//! `console.rs`. Windows releases the keep-awake request when the process dies, so the machine is
//! never held awake by a hung Awake.

use std::ffi::OsString;
use std::os::windows::ffi::OsStrExt;
use std::os::windows::io::{AsRawHandle, FromRawHandle, OwnedHandle};
use std::path::Path;
use std::sync::Arc;
use std::sync::atomic::{AtomicBool, AtomicI64, Ordering};
use std::thread::JoinHandle;
use std::time::Duration;
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
    CancelWaitableTimer, CreateEventW, CreateWaitableTimerExW, GetCurrentProcess, INFINITE, ResetEvent,
    SetEvent, SetWaitableTimer, TIMER_ALL_ACCESS, TerminateProcess, WaitForMultipleObjects,
    WaitForSingleObject,
};
use windows_sys::Win32::UI::WindowsAndMessaging::{PostMessageW, WM_APP};

use super::clock::SystemClock;
use super::{last_error, wide::wide};

pub const WM_WAITER: u32 = WM_APP + 1;
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
    timer: OwnedHandle,
    /// The armed deadline in ticks, `NO_DEADLINE` when cancelled.
    deadline: Arc<AtomicI64>,
    settings_pending: Arc<AtomicBool>,
    thread: Option<JoinHandle<()>>,
}

const NO_DEADLINE: i64 = i64::MIN;

impl Waiter {
    pub fn start(setup: WaiterSetup) -> io::Result<Waiter> {
        let stop = event(true)?;
        // SAFETY: anonymous synchronization timer; null attributes and name are allowed.
        let timer = unsafe { CreateWaitableTimerExW(ptr::null(), ptr::null(), 0, TIMER_ALL_ACCESS) };
        if timer.is_null() {
            return Err(last_error());
        }
        // SAFETY: fresh handle we own.
        let timer = unsafe { OwnedHandle::from_raw_handle(timer) };
        let settings_pending = Arc::new(AtomicBool::new(false));
        let deadline = Arc::new(AtomicI64::new(NO_DEADLINE));

        let watch = setup
            .watch
            .as_ref()
            .and_then(|(dir, file)| match DirectoryWatch::start(dir, file) {
                Ok(watch) => Some(watch),
                Err(error) => {
                    log::warn!("Not watching {} for changes yet: {error}", dir.display());
                    None
                }
            });
        let context = Context {
            window: setup.window as usize,
            stop: stop.try_clone()?,
            exit_event: setup.exit_event,
            timer: timer.try_clone()?,
            deadline: deadline.clone(),
            deadline_ends_process: setup.deadline_ends_process,
            process: setup.process,
            watch,
            watch_target: setup.watch,
            settings_pending: settings_pending.clone(),
        };
        let thread = std::thread::Builder::new()
            .name("awake-waiter".into())
            .spawn(move || context.run())?;
        Ok(Waiter {
            stop,
            timer,
            deadline,
            settings_pending,
            thread: Some(thread),
        })
    }

    /// Arms the one-shot deadline at an absolute time, which Windows keeps correct across clock
    /// changes and sleep. `None` cancels it.
    pub fn set_deadline(&self, deadline: Option<Timestamp>) {
        let handle = self.timer.as_raw_handle();
        match deadline {
            Some(at) => {
                self.deadline.store(at.ticks(), Ordering::Release);
                arm_timer(handle, at.ticks());
            }
            None => {
                self.deadline.store(NO_DEADLINE, Ordering::Release);
                // SAFETY: valid timer handle.
                unsafe { CancelWaitableTimer(handle) };
            }
        }
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
    deadline_ends_process: bool,
    process: Option<OwnedHandle>,
    watch: Option<DirectoryWatch>,
    /// What to watch, kept so the watch can be restored if the folder is deleted and recreated.
    watch_target: Option<(std::path::PathBuf, OsString)>,
    settings_pending: Arc<AtomicBool>,
}

impl Context {
    fn run(mut self) {
        loop {
            let mut handles: Vec<HANDLE> = vec![
                self.stop.as_raw_handle(),
                self.exit_event.as_raw_handle(),
                self.timer.as_raw_handle(),
            ];
            let process_index = self.process.as_ref().map(|p| {
                handles.push(p.as_raw_handle());
                handles.len() - 1
            });
            let watch_index = self.watch.as_ref().map(|w| {
                handles.push(w.event.as_raw_handle());
                handles.len() - 1
            });

            // Without a working watch, wake up now and then to try again.
            let timeout = if self.watch.is_none() && self.watch_target.is_some() {
                WATCH_RETRY.as_millis() as u32
            } else {
                INFINITE
            };
            // SAFETY: every handle in the list is owned by this context and stays open.
            let result =
                unsafe { WaitForMultipleObjects(handles.len() as u32, handles.as_ptr(), 0, timeout) };
            if result == WAIT_TIMEOUT {
                self.restore_watch();
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
                    if self.deadline_is_early() {
                        continue;
                    }
                    self.post(WaitEvent::DeadlineReached);
                    if self.deadline_ends_process {
                        self.watchdog();
                        return;
                    }
                }
                i if Some(i) == process_index => {
                    self.process = None;
                    self.post(WaitEvent::ProcessExited);
                    self.watchdog();
                    return;
                }
                i if Some(i) == watch_index => {
                    let watch = self.watch.as_mut().expect("index came from the watch");
                    let relevant = watch.take_relevant_change();
                    let rearmed = watch.arm();
                    if relevant && !self.settings_pending.swap(true, Ordering::AcqRel) {
                        self.post(WaitEvent::SettingsChanged);
                    }
                    if let Err(error) = rearmed {
                        log::warn!("Lost the settings watch, will retry: {error}");
                        self.watch = None;
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

    fn restore_watch(&mut self) {
        let Some((dir, file)) = &self.watch_target else {
            return;
        };
        if let Ok(watch) = DirectoryWatch::start(dir, file) {
            log::info!("Watching {} again.", dir.display());
            self.watch = Some(watch);
            // Changes made while unwatched would otherwise go unnoticed.
            if !self.settings_pending.swap(true, Ordering::AcqRel) {
                self.post(WaitEvent::SettingsChanged);
            }
        }
    }

    /// The timer runs on interrupt time, which clock corrections do not move, so it can fire
    /// while the system clock still reads a little before the deadline. Arms it again then.
    fn deadline_is_early(&self) -> bool {
        let deadline = self.deadline.load(Ordering::Acquire);
        if deadline == NO_DEADLINE || SystemClock.now_utc().ticks() >= deadline {
            return false;
        }
        arm_timer(self.timer.as_raw_handle(), deadline);
        true
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

fn event(manual_reset: bool) -> io::Result<OwnedHandle> {
    // SAFETY: anonymous event; null attributes and name are allowed.
    let raw = unsafe { CreateEventW(ptr::null(), i32::from(manual_reset), 0, ptr::null()) };
    if raw.is_null() {
        return Err(last_error());
    }
    // SAFETY: fresh handle we own.
    Ok(unsafe { OwnedHandle::from_raw_handle(raw) })
}

/// Overlapped `ReadDirectoryChangesW` on one directory, filtered to one file name.
struct DirectoryWatch {
    directory: OwnedHandle,
    event: OwnedHandle,
    file_name: Vec<u16>,
    // Boxed so their addresses stay fixed while the kernel writes into them.
    overlapped: Box<OVERLAPPED>,
    buffer: Box<[u32; 2048]>,
    armed: bool,
}

// SAFETY: the raw pointers inside OVERLAPPED are only used by the owning thread.
unsafe impl Send for DirectoryWatch {}

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
            event: event(true)?,
            file_name: file_name.encode_wide().map(lower).collect(),
            // SAFETY: OVERLAPPED is plain data.
            overlapped: Box::new(unsafe { mem::zeroed() }),
            buffer: Box::new([0; 2048]),
            armed: false,
        };
        watch.arm()?;
        Ok(watch)
    }

    fn arm(&mut self) -> io::Result<()> {
        // SAFETY: valid event handle.
        unsafe { ResetEvent(self.event.as_raw_handle()) };
        // SAFETY: OVERLAPPED is plain data.
        *self.overlapped = unsafe { mem::zeroed() };
        self.overlapped.hEvent = self.event.as_raw_handle();
        let filter = FILE_NOTIFY_CHANGE_FILE_NAME
            | FILE_NOTIFY_CHANGE_LAST_WRITE
            | FILE_NOTIFY_CHANGE_SIZE
            | FILE_NOTIFY_CHANGE_CREATION;
        // SAFETY: the buffer and OVERLAPPED are heap-allocated and outlive the request: `Drop`
        // cancels it and waits for completion before they are freed.
        let ok = unsafe {
            ReadDirectoryChangesW(
                self.directory.as_raw_handle(),
                self.buffer.as_mut_ptr().cast(),
                mem::size_of_val(&*self.buffer) as u32,
                0,
                filter,
                ptr::null_mut(),
                &mut *self.overlapped,
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
        // SAFETY: the request completed (its event is signaled); no wait is needed.
        let ok =
            unsafe { GetOverlappedResult(self.directory.as_raw_handle(), &*self.overlapped, &mut bytes, 0) }
                != 0;
        self.armed = false;
        if !ok || bytes == 0 {
            return true;
        }
        let base = self.buffer.as_ptr().cast::<u8>();
        let limit = (bytes as usize).min(mem::size_of_val(&*self.buffer));
        let mut offset = 0usize;
        loop {
            if offset + mem::size_of::<FILE_NOTIFY_INFORMATION>() > limit {
                return false;
            }
            // SAFETY: the kernel wrote well-formed, DWORD-aligned records inside `limit` bytes.
            let record = unsafe { &*base.add(offset).cast::<FILE_NOTIFY_INFORMATION>() };
            let name_len = record.FileNameLength as usize / 2;
            let name_start = offset + mem::offset_of!(FILE_NOTIFY_INFORMATION, FileName);
            if name_start + name_len * 2 > limit {
                return false;
            }
            // SAFETY: bounds checked just above.
            let name = unsafe { slice::from_raw_parts(base.add(name_start).cast::<u16>(), name_len) };
            if name.len() == self.file_name.len()
                && name.iter().map(|&c| lower(c)).eq(self.file_name.iter().copied())
            {
                return true;
            }
            if record.NextEntryOffset == 0 {
                return false;
            }
            offset += record.NextEntryOffset as usize;
        }
    }
}

impl Drop for DirectoryWatch {
    fn drop(&mut self) {
        if self.armed {
            let mut bytes = 0;
            // SAFETY: cancel the pending request and wait for the kernel to release the buffer.
            unsafe {
                CancelIoEx(self.directory.as_raw_handle(), &*self.overlapped);
                GetOverlappedResult(self.directory.as_raw_handle(), &*self.overlapped, &mut bytes, 1);
            }
        }
    }
}

fn lower(unit: u16) -> u16 {
    if (u16::from(b'A')..=u16::from(b'Z')).contains(&unit) {
        unit + 32
    } else {
        unit
    }
}
