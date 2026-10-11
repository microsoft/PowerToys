//! Other processes: the one a session is bound to, and the detached Awake a hand-off starts.
//!
//! A bound process's handle is opened once, at startup, so a later PID reuse cannot redirect
//! the wait. The only window for reuse is between whoever computed the PID and that open; a
//! target created after Awake itself cannot be the intended one, and is rejected.

use std::os::windows::io::{AsRawHandle, FromRawHandle, OwnedHandle};
use std::path::Path;
use std::time::Duration;
use std::{io, mem, ptr};

use windows_sys::Win32::Foundation::{
    ERROR_ACCESS_DENIED, FILETIME, GetLastError, HANDLE, INVALID_HANDLE_VALUE, WAIT_OBJECT_0, WAIT_TIMEOUT,
};
use windows_sys::Win32::System::Diagnostics::ToolHelp::{
    CreateToolhelp32Snapshot, PROCESSENTRY32W, Process32FirstW, Process32NextW, TH32CS_SNAPPROCESS,
};
use windows_sys::Win32::System::Threading::{
    CREATE_NEW_CONSOLE, CreateProcessW, DETACHED_PROCESS, GetCurrentProcess, GetCurrentProcessId,
    GetExitCodeProcess, GetProcessTimes, OpenProcess, PROCESS_INFORMATION, PROCESS_NAME_WIN32,
    PROCESS_QUERY_LIMITED_INFORMATION, PROCESS_SYNCHRONIZE, QueryFullProcessImageNameW, STARTUPINFOW,
    WaitForMultipleObjects, WaitForSingleObject,
};

use super::last_error;
use super::wide::{from_buffer, wide};

#[derive(Debug)]
pub struct BoundProcess {
    pub handle: OwnedHandle,
    pub target: BoundTarget,
}

/// What Awake shows about the process it is bound to; the handle stays with the waiter.
#[derive(Clone, Debug, PartialEq, Eq)]
pub struct BoundTarget {
    pub pid: u32,
    pub image_name: Option<String>,
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum BindError {
    NotFound,
    IsSelf,
    AccessDenied,
    ParentNotFound,
}

pub fn bind_pid(pid: i64) -> Result<BoundProcess, BindError> {
    let pid = u32::try_from(pid).map_err(|_| BindError::NotFound)?;
    // SAFETY: no preconditions.
    if pid == unsafe { GetCurrentProcessId() } {
        return Err(BindError::IsSelf);
    }
    open_older_than_self(pid)
}

/// Binds to `parent`, the result of [`parent_pid`]; the caller looks it up once, since taking a
/// process snapshot walks every process on the machine.
pub fn bind_parent(parent: Option<u32>) -> Result<BoundProcess, BindError> {
    let parent = parent.ok_or(BindError::ParentNotFound)?;
    open_older_than_self(parent).map_err(|error| match error {
        BindError::AccessDenied => BindError::AccessDenied,
        _ => BindError::ParentNotFound,
    })
}

fn open_older_than_self(pid: u32) -> Result<BoundProcess, BindError> {
    let handle = open(pid)?;
    // SAFETY: valid process handle with SYNCHRONIZE access.
    if unsafe { WaitForSingleObject(handle.as_raw_handle(), 0) } != WAIT_TIMEOUT {
        return Err(BindError::NotFound);
    }
    // SAFETY: GetCurrentProcess returns a pseudo handle that never needs closing.
    let own_start = creation_time(unsafe { GetCurrentProcess() });
    if let (Some(target), Some(own)) = (creation_time(handle.as_raw_handle()), own_start)
        && target > own
    {
        return Err(BindError::NotFound);
    }
    let image_name = image_name(handle.as_raw_handle());
    Ok(BoundProcess {
        handle,
        target: BoundTarget { pid, image_name },
    })
}

fn open(pid: u32) -> Result<OwnedHandle, BindError> {
    for access in [
        PROCESS_SYNCHRONIZE | PROCESS_QUERY_LIMITED_INFORMATION,
        PROCESS_SYNCHRONIZE,
    ] {
        // SAFETY: plain value call.
        let raw = unsafe { OpenProcess(access, 0, pid) };
        if !raw.is_null() {
            // SAFETY: fresh handle we own.
            return Ok(unsafe { OwnedHandle::from_raw_handle(raw) });
        }
        // SAFETY: no preconditions.
        if unsafe { GetLastError() } != ERROR_ACCESS_DENIED {
            return Err(BindError::NotFound);
        }
    }
    Err(BindError::AccessDenied)
}

fn creation_time(process: HANDLE) -> Option<u64> {
    let mut times = [FILETIME {
        dwLowDateTime: 0,
        dwHighDateTime: 0,
    }; 4];
    let [created, exited, kernel, user] = &mut times;
    // SAFETY: all out-pointers are valid for writes.
    let ok = unsafe { GetProcessTimes(process, created, exited, kernel, user) } != 0;
    ok.then(|| (u64::from(created.dwHighDateTime) << 32) | u64::from(created.dwLowDateTime))
}

fn image_name(process: HANDLE) -> Option<String> {
    let mut buffer = [0u16; 1024];
    let mut len = buffer.len() as u32;
    // SAFETY: `buffer` holds `len` units.
    let ok =
        unsafe { QueryFullProcessImageNameW(process, PROCESS_NAME_WIN32, buffer.as_mut_ptr(), &mut len) }
            != 0;
    ok.then(|| from_buffer(&buffer[..len as usize])).and_then(|path| {
        Path::new(&path)
            .file_name()
            .map(|name| name.to_string_lossy().into_owned())
    })
}

/// The PID that started this process, from a process snapshot.
pub fn parent_pid() -> Option<u32> {
    // SAFETY: plain value call.
    let snapshot = unsafe { CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0) };
    if snapshot == INVALID_HANDLE_VALUE || snapshot.is_null() {
        return None;
    }
    // SAFETY: fresh handle we own.
    let snapshot = unsafe { OwnedHandle::from_raw_handle(snapshot) };
    // SAFETY: no preconditions.
    let own = unsafe { GetCurrentProcessId() };
    // SAFETY: PROCESSENTRY32W is plain data; zero is a valid starting value.
    let mut entry: PROCESSENTRY32W = unsafe { mem::zeroed() };
    entry.dwSize = mem::size_of::<PROCESSENTRY32W>() as u32;
    // SAFETY: `entry` is valid and sized.
    let mut more = unsafe { Process32FirstW(snapshot.as_raw_handle(), &mut entry) } != 0;
    while more {
        if entry.th32ProcessID == own {
            return (entry.th32ParentProcessID != 0).then_some(entry.th32ParentProcessID);
        }
        // SAFETY: as above.
        more = unsafe { Process32NextW(snapshot.as_raw_handle(), &mut entry) } != 0;
    }
    None
}

/// Starts Awake again with `args`, detached from the calling console, and returns at once. The
/// original Awake options never blocked the shell that started them, and scripts rely on that.
/// `new_console` gives the new process a console window of its own, as unbound sessions had;
/// bound sessions run without any console, as the .NET Awake did, so no console host runs
/// alongside them.
pub fn spawn_detached(args: &[String], new_console: bool) -> io::Result<OwnedHandle> {
    let exe = std::env::current_exe()?;
    let mut command_line: Vec<u16> = std::iter::once(quote(&exe.to_string_lossy()))
        .chain(args.iter().map(|a| quote(a)))
        .collect::<Vec<_>>()
        .join(" ")
        .encode_utf16()
        .chain(Some(0))
        .collect();
    // SAFETY: STARTUPINFOW and PROCESS_INFORMATION are plain data.
    let mut startup: STARTUPINFOW = unsafe { mem::zeroed() };
    startup.cb = mem::size_of::<STARTUPINFOW>() as u32;
    // SAFETY: as above.
    let mut process: PROCESS_INFORMATION = unsafe { mem::zeroed() };
    let exe = wide(exe);
    let flags = if new_console {
        CREATE_NEW_CONSOLE
    } else {
        DETACHED_PROCESS
    };
    // SAFETY: all pointers are valid; the command line buffer is writable as required. No
    // handles are inherited and no standard handles are passed, so the child uses its own.
    let ok = unsafe {
        CreateProcessW(
            exe.as_ptr(),
            command_line.as_mut_ptr(),
            ptr::null(),
            ptr::null(),
            0,
            flags,
            ptr::null(),
            ptr::null(),
            &startup,
            &mut process,
        )
    } != 0;
    if !ok {
        return Err(last_error());
    }
    // SAFETY: both handles are ours; the thread handle is closed and the process returned.
    unsafe {
        drop(OwnedHandle::from_raw_handle(process.hThread));
        Ok(OwnedHandle::from_raw_handle(process.hProcess))
    }
}

/// How a process started with [`spawn_detached`] got on.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Started {
    /// It signaled `ready`.
    Confirmed,
    /// It exited first, with this exit code.
    Exited(i32),
    /// Neither happened within the time given.
    NoWord,
}

/// Waits up to `timeout` for `child` to signal `ready` or to exit.
pub fn wait_for_start(ready: &OwnedHandle, child: &OwnedHandle, timeout: Duration) -> Started {
    let handles = [ready.as_raw_handle(), child.as_raw_handle()];
    let millis = u32::try_from(timeout.as_millis()).unwrap_or(u32::MAX - 1);
    // SAFETY: both handles are open for the duration of the call.
    let result = unsafe { WaitForMultipleObjects(2, handles.as_ptr(), 0, millis) };
    match result {
        r if r == WAIT_OBJECT_0 => Started::Confirmed,
        r if r == WAIT_OBJECT_0 + 1 => {
            let mut code = 1u32;
            // SAFETY: the child has exited; its handle is valid.
            unsafe { GetExitCodeProcess(child.as_raw_handle(), &mut code) };
            Started::Exited(code as i32)
        }
        _ => Started::NoWord,
    }
}

/// Quotes one argument for the command line, following the rules `CommandLineToArgvW` reverses.
fn quote(argument: &str) -> String {
    if !argument.is_empty() && !argument.contains([' ', '\t', '"']) {
        return argument.to_owned();
    }
    let mut quoted = String::from('"');
    let mut backslashes = 0;
    for c in argument.chars() {
        match c {
            '\\' => backslashes += 1,
            '"' => {
                quoted.extend(std::iter::repeat_n('\\', backslashes * 2 + 1));
                quoted.push('"');
                backslashes = 0;
            }
            _ => {
                quoted.extend(std::iter::repeat_n('\\', backslashes));
                quoted.push(c);
                backslashes = 0;
            }
        }
    }
    quoted.extend(std::iter::repeat_n('\\', backslashes * 2));
    quoted.push('"');
    quoted
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn quoting_round_trips_through_the_windows_rules() {
        assert_eq!(quote("for"), "for");
        assert_eq!(quote("C:\\Program Files\\x.exe"), "\"C:\\Program Files\\x.exe\"");
        assert_eq!(quote(""), "\"\"");
        assert_eq!(quote("a\"b"), "\"a\\\"b\"");
        assert_eq!(quote("dir\\ end\\"), "\"dir\\ end\\\\\"");
    }

    #[test]
    fn a_start_is_confirmed_by_the_event_or_ends_with_the_exit_code() {
        let ready = crate::platform::event::manual_reset(None).unwrap();
        let mut child = std::process::Command::new("cmd.exe")
            .args(["/c", "exit 7"])
            .spawn()
            .unwrap();
        child.wait().unwrap();
        let child: OwnedHandle = child.into();
        assert_eq!(
            wait_for_start(&ready, &child, Duration::from_secs(5)),
            Started::Exited(7)
        );
        // SAFETY: valid event handle.
        unsafe { windows_sys::Win32::System::Threading::SetEvent(ready.as_raw_handle()) };
        assert_eq!(
            wait_for_start(&ready, &child, Duration::ZERO),
            Started::Confirmed,
            "the event wins when both are signaled"
        );
        let silent = crate::platform::event::manual_reset(None).unwrap();
        let other = crate::platform::event::manual_reset(None).unwrap();
        assert_eq!(wait_for_start(&silent, &other, Duration::ZERO), Started::NoWord);
    }

    #[test]
    fn binding_to_self_is_rejected() {
        let own = i64::from(std::process::id());
        assert_eq!(bind_pid(own).unwrap_err(), BindError::IsSelf);
    }

    #[test]
    fn invalid_pids_are_not_found() {
        assert_eq!(bind_pid(-1).unwrap_err(), BindError::NotFound);
        assert_eq!(
            bind_pid(i64::from(u32::MAX) + 1).unwrap_err(),
            BindError::NotFound
        );
        assert_eq!(bind_pid(0x7FFF_FFF0).unwrap_err(), BindError::NotFound);
    }

    #[test]
    fn the_test_runner_parent_can_be_bound() {
        let bound = bind_parent(parent_pid()).expect("cargo test has a parent process");
        assert_ne!(bound.target.pid, std::process::id());
        assert!(bound.target.image_name.is_some());
    }

    #[test]
    fn a_missing_parent_is_reported_as_such() {
        assert_eq!(bind_parent(None).unwrap_err(), BindError::ParentNotFound);
    }

    #[test]
    fn a_process_created_after_us_is_rejected() {
        let mut child = std::process::Command::new("cmd.exe")
            .args(["/c", "ping -n 3 127.0.0.1 >nul"])
            .spawn()
            .unwrap();
        let result = bind_pid(i64::from(child.id()));
        child.kill().ok();
        child.wait().ok();
        assert_eq!(
            result.unwrap_err(),
            BindError::NotFound,
            "children are newer than us, like a reused PID would be"
        );
    }
}
