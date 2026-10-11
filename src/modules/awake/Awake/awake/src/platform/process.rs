//! Binding Awake's lifetime to another process.
//!
//! The process handle is opened once, at startup, so a later PID reuse cannot redirect the
//! wait. The only window for reuse is between whoever computed the PID and that open; a target
//! created after Awake itself cannot be the intended one, and is rejected.

use std::mem;
use std::os::windows::io::{AsRawHandle, FromRawHandle, OwnedHandle};
use std::path::Path;

use windows_sys::Win32::Foundation::{
    ERROR_ACCESS_DENIED, FILETIME, GetLastError, HANDLE, INVALID_HANDLE_VALUE, WAIT_TIMEOUT,
};
use windows_sys::Win32::System::Diagnostics::ToolHelp::{
    CreateToolhelp32Snapshot, PROCESSENTRY32W, Process32FirstW, Process32NextW, TH32CS_SNAPPROCESS,
};
use windows_sys::Win32::System::Threading::{
    GetCurrentProcess, GetCurrentProcessId, GetProcessTimes, OpenProcess, PROCESS_NAME_WIN32,
    PROCESS_QUERY_LIMITED_INFORMATION, PROCESS_SYNCHRONIZE, QueryFullProcessImageNameW, WaitForSingleObject,
};

use super::wide::from_buffer;

#[derive(Debug)]
pub struct BoundProcess {
    pub handle: OwnedHandle,
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

pub fn bind_parent() -> Result<BoundProcess, BindError> {
    let parent = parent_pid().ok_or(BindError::ParentNotFound)?;
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
        pid,
        image_name,
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

#[cfg(test)]
mod tests {
    use super::*;

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
        let bound = bind_parent().expect("cargo test has a parent process");
        assert_ne!(bound.pid, std::process::id());
        assert!(bound.image_name.is_some());
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
