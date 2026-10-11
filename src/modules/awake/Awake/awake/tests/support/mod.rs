//! Helpers for driving the real executable in an isolated sandbox.

#![allow(dead_code)]

pub mod runner;

use std::ffi::OsString;
use std::io::Read;
use std::ops::{Deref, DerefMut};
use std::os::windows::io::{AsRawHandle, FromRawHandle, OwnedHandle};
use std::os::windows::process::CommandExt;
use std::path::{Path, PathBuf};
use std::process::{Child, Command, Output, Stdio};
use std::sync::mpsc;
use std::time::{Duration, Instant};
use std::{fs, mem, ptr};

use windows_sys::Win32::Foundation::{CloseHandle, HANDLE, HWND};
use windows_sys::Win32::System::Console::{CTRL_BREAK_EVENT, GenerateConsoleCtrlEvent};
use windows_sys::Win32::System::Diagnostics::ToolHelp::{
    CreateToolhelp32Snapshot, PROCESSENTRY32W, Process32FirstW, Process32NextW, TH32CS_SNAPPROCESS,
};
use windows_sys::Win32::System::Power::{CallNtPowerInformation, SystemExecutionState};
use windows_sys::Win32::System::ProcessStatus::{K32GetProcessMemoryInfo, PROCESS_MEMORY_COUNTERS_EX};
use windows_sys::Win32::System::Threading::{
    CreateEventW, DETACHED_PROCESS, EVENT_MODIFY_STATE, GR_GDIOBJECTS, GR_USEROBJECTS, GetGuiResources,
    GetProcessHandleCount, OpenEventW, SetEvent,
};
use windows_sys::Win32::UI::WindowsAndMessaging::{
    ENDSESSION_CLOSEAPP, FindWindowExW, GetWindowThreadProcessId, PostMessageW, SMTO_ABORTIFHUNG,
    SendMessageTimeoutW, WM_CLOSE, WM_ENDSESSION, WM_NULL,
};

pub const EXE: &str = env!("CARGO_BIN_EXE_awake");
const EXIT_EVENT: &str = "Local\\PowerToysAwakeExitEvent-c0d5e305-35fc-4fb5-83ec-f6070cfaf7fe";

pub fn wide(text: &str) -> Vec<u16> {
    text.encode_utf16().chain(Some(0)).collect()
}

/// A private `%LOCALAPPDATA%\Microsoft\PowerToys` and set of kernel object names.
pub struct Sandbox {
    pub root: PathBuf,
}

impl Sandbox {
    pub fn new(name: &str) -> Sandbox {
        let root = std::env::temp_dir().join(format!("awake-it-{name}-{}", std::process::id()));
        let _ = fs::remove_dir_all(&root);
        fs::create_dir_all(&root).unwrap();
        // Tests match English messages; without this file Awake would follow the machine's
        // Windows UI language once localized resources ship.
        fs::write(root.join("language.json"), br#"{"language":"en-US"}"#).unwrap();
        Sandbox { root }
    }

    pub fn settings_path(&self) -> PathBuf {
        self.root.join("Awake").join("settings.json")
    }

    /// Starts Awake without a console, as the PowerToys runner does, but with its output piped
    /// to the test. The runner passes no standard handles at all; [`Sandbox::launch_like_runner`]
    /// starts it exactly that way.
    pub fn command(&self, args: &[&str]) -> Command {
        let mut command = self.command_in_callers_console(args);
        command.creation_flags(DETACHED_PROCESS);
        command
    }

    /// Starts Awake exactly as the runner does: `flags` for creation, no standard handles and no
    /// inherited handles.
    pub fn launch_like_runner(&self, args: &[&str], flags: u32) -> runner::Launched {
        let env = [
            ("POWERTOYS_AWAKE_DATA_DIR", self.root.clone().into_os_string()),
            (
                "POWERTOYS_AWAKE_OBJECT_SUFFIX",
                OsString::from(self.object_suffix()),
            ),
        ];
        runner::launch(EXE, args, &env, flags).expect("start awake")
    }

    /// Starts Awake in the test's own console, the way a shell would.
    pub fn command_in_callers_console(&self, args: &[&str]) -> Command {
        let mut command = Command::new(EXE);
        command
            .args(args)
            .env("POWERTOYS_AWAKE_DATA_DIR", &self.root)
            .env("POWERTOYS_AWAKE_OBJECT_SUFFIX", self.object_suffix())
            .stdin(Stdio::null())
            .stdout(Stdio::piped())
            .stderr(Stdio::piped());
        command
    }

    pub fn spawn(&self, args: &[&str]) -> Running {
        Running::new(self.command(args).spawn().expect("start awake"))
    }

    pub fn log(&self) -> String {
        let dir = self.root.join("Awake").join("Logs");
        let mut text = String::new();
        for version in fs::read_dir(&dir).into_iter().flatten().flatten() {
            for file in fs::read_dir(version.path()).into_iter().flatten().flatten() {
                text.push_str(&fs::read_to_string(file.path()).unwrap_or_default());
            }
        }
        text
    }

    /// Waits until the log contains `needle` `count` times.
    pub fn wait_for_log(&self, needle: &str, count: usize, timeout: Duration) -> bool {
        let deadline = Instant::now() + timeout;
        while Instant::now() < deadline {
            if self.log().matches(needle).count() >= count {
                return true;
            }
            std::thread::sleep(Duration::from_millis(25));
        }
        false
    }

    pub fn object_suffix(&self) -> String {
        format!("-test-{:016x}", fnv1a(self.root.as_os_str().as_encoded_bytes()))
    }

    pub fn exit_event_name(&self) -> String {
        format!("{EXIT_EVENT}{}", self.object_suffix())
    }

    pub fn signal_exit(&self) {
        assert!(self.try_signal_exit(), "exit event exists while Awake runs");
    }

    /// Creates the exit event already set, as the runner leaves it for a few seconds after
    /// stopping the Awake it started. It lasts as long as the returned handle.
    pub fn signaled_exit_event(&self) -> OwnedHandle {
        let name = wide(&self.exit_event_name());
        // SAFETY: NUL-terminated name; a manual-reset event created set, like the runner's.
        let event = unsafe { CreateEventW(ptr::null(), 1, 1, name.as_ptr()) };
        assert!(!event.is_null(), "{}", std::io::Error::last_os_error());
        // SAFETY: a fresh handle we own.
        let event = unsafe { OwnedHandle::from_raw_handle(event) };
        // SAFETY: valid event handle; it may have existed unset already.
        unsafe { SetEvent(event.as_raw_handle()) };
        event
    }

    /// Sets the exit event if an Awake in this sandbox has created it.
    fn try_signal_exit(&self) -> bool {
        let name = wide(&self.exit_event_name());
        // SAFETY: NUL-terminated name; our handle is closed after use, while Awake keeps its own.
        unsafe {
            let event = OpenEventW(EVENT_MODIFY_STATE, 0, name.as_ptr());
            if event.is_null() {
                return false;
            }
            SetEvent(event);
            CloseHandle(event);
            true
        }
    }

    /// Writes the file the way the runner and the .NET writers do: truncate and rewrite in place.
    pub fn write_settings(&self, json: &str) {
        let path = self.settings_path();
        fs::create_dir_all(path.parent().unwrap()).unwrap();
        for _ in 0..50 {
            if fs::write(&path, json).is_ok() {
                return;
            }
            std::thread::sleep(Duration::from_millis(10));
        }
        panic!("could not write {}", path.display());
    }
}

impl Drop for Sandbox {
    /// Stops a detached Awake that a failed hand-off test left behind (children are killed by
    /// [`Running`]), then gives it a moment to release its log file before removing the folder.
    fn drop(&mut self) {
        self.try_signal_exit();
        let deadline = Instant::now() + Duration::from_secs(5);
        while fs::remove_dir_all(&self.root).is_err() && self.root.exists() && Instant::now() < deadline {
            std::thread::sleep(Duration::from_millis(50));
        }
    }
}

/// A child process that is killed when dropped, so a failed assertion never leaves Awake holding
/// the machine awake, its single-instance mutex taken, or the sandbox's log file open. After a
/// clean exit, dropping it only reaps the process.
pub struct Running(Option<Child>);

impl Running {
    pub fn new(child: Child) -> Running {
        Running(Some(child))
    }

    /// Collects the remaining output; call it once the process has exited (see [`wait_exit`]).
    pub fn wait_with_output(mut self) -> std::io::Result<Output> {
        self.0.take().expect("running child").wait_with_output()
    }
}

impl Deref for Running {
    type Target = Child;

    fn deref(&self) -> &Child {
        self.0.as_ref().expect("running child")
    }
}

impl DerefMut for Running {
    fn deref_mut(&mut self) -> &mut Child {
        self.0.as_mut().expect("running child")
    }
}

impl Drop for Running {
    fn drop(&mut self) {
        if let Some(child) = &mut self.0 {
            if matches!(child.try_wait(), Ok(None)) {
                let _ = child.kill();
            }
            let _ = child.wait();
        }
    }
}

/// Like [`Command::output`], but fails the test after `timeout` instead of hanging it, and kills
/// the process. Use it wherever a regression could make Awake wait on the test that waits on it.
pub fn output_within(command: &mut Command, timeout: Duration) -> Output {
    let deadline = Instant::now() + timeout;
    let mut child = Running::new(command.spawn().expect("start awake"));
    // Drain both pipes while waiting, so a chatty process cannot block on a full pipe.
    let stdout = read_in_background(child.stdout.take());
    let stderr = read_in_background(child.stderr.take());
    let status = loop {
        if let Some(status) = child.try_wait().unwrap() {
            break status;
        }
        assert!(
            Instant::now() < deadline,
            "{command:?} did not exit within {timeout:?}"
        );
        std::thread::sleep(Duration::from_millis(20));
    };
    // The pipes close at exit unless a process that inherited them is still running.
    let drained = |pipe: mpsc::Receiver<Vec<u8>>, name: &str| {
        pipe.recv_timeout(Duration::from_secs(5))
            .unwrap_or_else(|_| panic!("the {name} of {command:?} stayed open after it exited"))
    };
    Output {
        status,
        stdout: drained(stdout, "stdout"),
        stderr: drained(stderr, "stderr"),
    }
}

fn read_in_background(pipe: Option<impl Read + Send + 'static>) -> mpsc::Receiver<Vec<u8>> {
    let (sender, receiver) = mpsc::channel();
    std::thread::spawn(move || {
        let mut bytes = Vec::new();
        if let Some(mut pipe) = pipe {
            let _ = pipe.read_to_end(&mut bytes);
        }
        let _ = sender.send(bytes);
    });
    receiver
}

pub fn settings_json(mode: i64, display: bool, hours: u32, minutes: u32, expiration: &str) -> String {
    format!(
        r#"{{"name":"Awake","version":"1.0","properties":{{"keepDisplayOn":{display},"mode":{mode},"intervalHours":{hours},"intervalMinutes":{minutes},"expirationDateTime":"{expiration}","customTrayTimes":{{}}}}}}"#
    )
}

fn fnv1a(bytes: &[u8]) -> u64 {
    bytes.iter().fold(0xcbf2_9ce4_8422_2325, |hash, &b| {
        (hash ^ u64::from(b)).wrapping_mul(0x0100_0000_01b3)
    })
}

pub fn wait_exit(child: &mut Child, timeout: Duration) -> Option<i32> {
    let deadline = Instant::now() + timeout;
    while Instant::now() < deadline {
        if let Some(status) = child.try_wait().unwrap() {
            return status.code();
        }
        std::thread::sleep(Duration::from_millis(20));
    }
    None
}

/// The hidden tray window of `pid`.
pub fn find_window(pid: u32, timeout: Duration) -> Option<HWND> {
    let class = wide("Awake.MessageWindow");
    let deadline = Instant::now() + timeout;
    while Instant::now() < deadline {
        let mut window: HWND = ptr::null_mut();
        loop {
            // SAFETY: iterating top-level windows by class name.
            window = unsafe { FindWindowExW(ptr::null_mut(), window, class.as_ptr(), ptr::null()) };
            if window.is_null() {
                break;
            }
            let mut owner = 0;
            // SAFETY: valid window handle from FindWindowExW.
            unsafe { GetWindowThreadProcessId(window, &mut owner) };
            if owner == pid {
                return Some(window);
            }
        }
        std::thread::sleep(Duration::from_millis(25));
    }
    None
}

/// Whether the window thread answers within `timeout`; a deadlocked thread would not.
pub fn responsive(window: HWND, timeout: Duration) -> bool {
    let mut result = 0;
    // SAFETY: sending WM_NULL with a timeout to another process's window.
    unsafe {
        SendMessageTimeoutW(
            window,
            WM_NULL,
            0,
            0,
            SMTO_ABORTIFHUNG,
            timeout.as_millis() as u32,
            &mut result,
        ) != 0
    }
}

pub fn close_window(window: HWND) {
    // SAFETY: posting to another process's window.
    unsafe { PostMessageW(window, WM_CLOSE, 0, 0) };
}

/// What the Restart Manager sends when an installer needs Awake's files: the session goes on.
pub fn end_session_for_restart_manager(window: HWND) {
    // SAFETY: posting to another process's window; the message carries no pointers.
    unsafe { PostMessageW(window, WM_ENDSESSION, 1, ENDSESSION_CLOSEAPP as isize) };
}

#[derive(Clone, Copy, Debug)]
pub struct Resources {
    pub private_bytes: usize,
    pub working_set: usize,
    pub handles: u32,
    pub gdi_objects: u32,
    pub user_objects: u32,
    pub threads: u32,
}

pub fn resources(child: &Child) -> Resources {
    let process: HANDLE = child.as_raw_handle();
    // SAFETY: plain data, valid process handle with full access (we created it).
    unsafe {
        let mut memory: PROCESS_MEMORY_COUNTERS_EX = mem::zeroed();
        memory.cb = mem::size_of::<PROCESS_MEMORY_COUNTERS_EX>() as u32;
        K32GetProcessMemoryInfo(process, (&raw mut memory).cast(), memory.cb);
        let mut handles = 0;
        GetProcessHandleCount(process, &mut handles);
        Resources {
            private_bytes: memory.PrivateUsage,
            working_set: memory.WorkingSetSize,
            handles,
            gdi_objects: GetGuiResources(process, GR_GDIOBJECTS),
            user_objects: GetGuiResources(process, GR_USEROBJECTS),
            threads: thread_count(child.id()),
        }
    }
}

fn thread_count(pid: u32) -> u32 {
    // SAFETY: snapshot handle closed below; entry is sized.
    unsafe {
        let snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        let mut entry: PROCESSENTRY32W = mem::zeroed();
        entry.dwSize = mem::size_of::<PROCESSENTRY32W>() as u32;
        let mut more = Process32FirstW(snapshot, &mut entry) != 0;
        let mut threads = 0;
        while more {
            if entry.th32ProcessID == pid {
                threads = entry.cntThreads;
                break;
            }
            more = Process32NextW(snapshot, &mut entry) != 0;
        }
        windows_sys::Win32::Foundation::CloseHandle(snapshot);
        threads
    }
}

/// The machine-wide execution state: what all processes together currently request.
pub fn system_execution_state() -> u32 {
    let mut state = 0u32;
    // SAFETY: the output buffer holds a u32, as SystemExecutionState returns.
    unsafe {
        CallNtPowerInformation(SystemExecutionState, ptr::null(), 0, (&raw mut state).cast(), 4);
    }
    state
}

/// Sends Ctrl+Break to a process started with `CREATE_NEW_PROCESS_GROUP` in this console.
pub fn ctrl_break(pid: u32) {
    // SAFETY: plain call; the group id is the process id of the group's first process.
    let sent = unsafe { GenerateConsoleCtrlEvent(CTRL_BREAK_EVENT, pid) } != 0;
    assert!(sent, "{}", std::io::Error::last_os_error());
}

/// A time `seconds` from now, as the settings file stores it.
pub fn utc_in(seconds: u64) -> String {
    let unix = std::time::SystemTime::now()
        .duration_since(std::time::UNIX_EPOCH)
        .unwrap()
        .as_secs()
        + seconds;
    let ticks = (unix as i64 + 11_644_473_600) * 10_000_000;
    awake_core::time::DateTimeOffset::new(awake_core::time::Timestamp::from_ticks(ticks), 0).to_json_string()
}

pub fn path_text(path: &Path) -> String {
    path.display().to_string()
}
