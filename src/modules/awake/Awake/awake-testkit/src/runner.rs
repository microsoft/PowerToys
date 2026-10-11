//! Starting a program the way the PowerToys runner starts Awake, and seeing what it brought along.
//!
//! `std::process::Command` always hands the child standard handles and lets it inherit handles,
//! so a child started with it never sees the console state the runner gives Awake. This calls
//! `CreateProcessW` as `AwakeModuleInterface/dllmain.cpp` does instead.

use std::ffi::OsString;
use std::os::windows::ffi::OsStrExt;
use std::os::windows::io::{AsHandle, AsRawHandle, BorrowedHandle, FromRawHandle, OwnedHandle};
use std::time::Duration;
use std::{io, mem, ptr};

use windows_sys::Win32::Foundation::{HANDLE, WAIT_OBJECT_0};
use windows_sys::Win32::System::Threading::{
    CREATE_UNICODE_ENVIRONMENT, CreateProcessW, GetExitCodeProcess, PROCESS_INFORMATION, ResumeThread,
    STARTUPINFOW, TerminateProcess, WaitForSingleObject,
};

/// A process started by [`launch`]; ended when dropped if it is still running.
#[derive(Debug)]
pub struct Launched {
    process: OwnedHandle,
    thread: OwnedHandle,
    /// Its process ID.
    pub pid: u32,
}

impl Launched {
    /// The process handle, with full access.
    pub fn handle(&self) -> HANDLE {
        self.process.as_raw_handle()
    }

    /// Lets a process started with `CREATE_SUSPENDED` run.
    pub fn resume(&self) {
        // SAFETY: valid thread handle with full access, since we created it.
        unsafe { ResumeThread(self.thread.as_raw_handle()) };
    }

    /// The exit code, once the process has exited within `timeout`.
    pub fn wait_exit(&self, timeout: Duration) -> Option<i32> {
        let millis = u32::try_from(timeout.as_millis()).unwrap_or(u32::MAX - 1);
        // SAFETY: valid process handle.
        if unsafe { WaitForSingleObject(self.handle(), millis) } != WAIT_OBJECT_0 {
            return None;
        }
        let mut code = 0u32;
        // SAFETY: valid process handle and out-pointer.
        unsafe { GetExitCodeProcess(self.handle(), &mut code) };
        Some(code as i32)
    }

    /// Ends the process and waits briefly for it to go.
    pub fn kill(&self) {
        // SAFETY: valid process handle with full access, since we created it.
        unsafe {
            TerminateProcess(self.handle(), 1);
            WaitForSingleObject(self.handle(), 5000);
        }
    }
}

impl AsHandle for Launched {
    fn as_handle(&self) -> BorrowedHandle<'_> {
        self.process.as_handle()
    }
}

impl Drop for Launched {
    fn drop(&mut self) {
        if self.wait_exit(Duration::ZERO).is_none() {
            self.kill();
        }
    }
}

/// Starts `exe` with `args` and creation `flags` as the runner does: no inherited handles and no
/// standard handles, so a `DETACHED_PROCESS` child has no console and no output at all. `env` is
/// added to this process's environment.
///
/// # Errors
///
/// The error from `CreateProcessW`.
pub fn launch(exe: &str, args: &[&str], env: &[(&str, OsString)], flags: u32) -> io::Result<Launched> {
    let mut command_line: Vec<u16> = std::iter::once(quote(exe))
        .chain(args.iter().map(|a| quote(a)))
        .collect::<Vec<_>>()
        .join(" ")
        .encode_utf16()
        .chain(Some(0))
        .collect();
    let environment = environment_block(env);
    let exe_wide: Vec<u16> = exe.encode_utf16().chain(Some(0)).collect();
    // SAFETY: STARTUPINFOW and PROCESS_INFORMATION are plain data.
    let mut startup: STARTUPINFOW = unsafe { mem::zeroed() };
    startup.cb = mem::size_of::<STARTUPINFOW>() as u32;
    // SAFETY: as above.
    let mut process: PROCESS_INFORMATION = unsafe { mem::zeroed() };
    // SAFETY: every pointer is valid for the call; the command line is writable as required.
    let ok = unsafe {
        CreateProcessW(
            exe_wide.as_ptr(),
            command_line.as_mut_ptr(),
            ptr::null(),
            ptr::null(),
            0,
            flags | CREATE_UNICODE_ENVIRONMENT,
            environment.as_ptr().cast(),
            ptr::null(),
            &startup,
            &mut process,
        )
    } != 0;
    if !ok {
        return Err(io::Error::last_os_error());
    }
    // SAFETY: both handles are fresh and ours.
    unsafe {
        Ok(Launched {
            process: OwnedHandle::from_raw_handle(process.hProcess),
            thread: OwnedHandle::from_raw_handle(process.hThread),
            pid: process.dwProcessId,
        })
    }
}

/// Wraps an argument with spaces in quotes; enough for the arguments the tests and `measure` use.
fn quote(argument: &str) -> String {
    if argument.is_empty() || argument.contains(' ') {
        format!("\"{argument}\"")
    } else {
        argument.to_owned()
    }
}

fn environment_block(extra: &[(&str, OsString)]) -> Vec<u16> {
    let mut variables: Vec<(OsString, OsString)> = std::env::vars_os()
        .filter(|(name, _)| !extra.iter().any(|(added, _)| name.eq_ignore_ascii_case(added)))
        .collect();
    variables.extend(
        extra
            .iter()
            .map(|(name, value)| (OsString::from(name), value.clone())),
    );
    // Windows expects the block sorted by name, ignoring case.
    variables.sort_by_key(|(name, _)| name.to_ascii_uppercase());
    let mut block = Vec::new();
    for (name, value) in variables {
        block.extend(name.encode_wide());
        block.push(u16::from(b'='));
        block.extend(value.encode_wide());
        block.push(0);
    }
    block.push(0);
    block
}
