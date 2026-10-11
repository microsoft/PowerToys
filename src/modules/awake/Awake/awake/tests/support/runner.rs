//! Starting a program the way the PowerToys runner starts Awake, and seeing what it brought along.
//!
//! `std::process::Command` always hands the child standard handles and lets it inherit handles,
//! so a child started with it never sees the console state the runner gives Awake. This calls
//! `CreateProcessW` as `AwakeModuleInterface/dllmain.cpp` does instead. Shared by the end-to-end
//! tests and the `measure` example, so it depends on nothing else in the test support.

#![allow(dead_code)]

use std::ffi::OsString;
use std::os::windows::ffi::OsStrExt;
use std::os::windows::io::{AsRawHandle, FromRawHandle, OwnedHandle};
use std::time::Duration;
use std::{io, mem, ptr};

use windows_sys::Win32::Foundation::{HANDLE, INVALID_HANDLE_VALUE, LocalFree, WAIT_OBJECT_0};
use windows_sys::Win32::System::Diagnostics::ToolHelp::{
    CreateToolhelp32Snapshot, PROCESSENTRY32W, Process32FirstW, Process32NextW, TH32CS_SNAPPROCESS,
    TH32CS_SNAPTHREAD, THREADENTRY32, Thread32First, Thread32Next,
};
use windows_sys::Win32::System::ProcessStatus::{K32GetProcessMemoryInfo, PROCESS_MEMORY_COUNTERS};
use windows_sys::Win32::System::Threading::{
    CREATE_UNICODE_ENVIRONMENT, CreateProcessW, GetExitCodeProcess, GetThreadDescription, OpenProcess,
    OpenThread, PROCESS_INFORMATION, PROCESS_QUERY_LIMITED_INFORMATION, PROCESS_VM_READ, STARTUPINFOW,
    THREAD_QUERY_LIMITED_INFORMATION, TerminateProcess, WaitForSingleObject,
};

/// A process started by [`launch`]; ended when dropped if it is still running.
pub struct Launched {
    process: OwnedHandle,
    pub pid: u32,
}

impl Launched {
    pub fn handle(&self) -> HANDLE {
        self.process.as_raw_handle()
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

    pub fn kill(&self) {
        // SAFETY: valid process handle with full access, since we created it.
        unsafe {
            TerminateProcess(self.handle(), 1);
            WaitForSingleObject(self.handle(), 5000);
        }
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
    // SAFETY: both handles are ours; the thread handle is closed right away.
    unsafe {
        drop(OwnedHandle::from_raw_handle(process.hThread));
        Ok(Launched {
            process: OwnedHandle::from_raw_handle(process.hProcess),
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

/// The `conhost.exe` processes started for process `pid`, which Windows makes its children.
pub fn console_hosts(pid: u32) -> Vec<u32> {
    let mut hosts = Vec::new();
    // SAFETY: snapshot iteration with a sized entry; the handle is closed below.
    unsafe {
        let snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if snapshot == INVALID_HANDLE_VALUE {
            return hosts;
        }
        let snapshot = OwnedHandle::from_raw_handle(snapshot);
        let mut entry: PROCESSENTRY32W = mem::zeroed();
        entry.dwSize = mem::size_of::<PROCESSENTRY32W>() as u32;
        let mut more = Process32FirstW(snapshot.as_raw_handle(), &mut entry) != 0;
        while more {
            if entry.th32ParentProcessID == pid && name_is(&entry.szExeFile, "conhost.exe") {
                hosts.push(entry.th32ProcessID);
            }
            more = Process32NextW(snapshot.as_raw_handle(), &mut entry) != 0;
        }
    }
    hosts
}

fn name_is(name: &[u16], expected: &str) -> bool {
    let end = name.iter().position(|&c| c == 0).unwrap_or(name.len());
    String::from_utf16_lossy(&name[..end]).eq_ignore_ascii_case(expected)
}

/// The names the threads of process `pid` gave themselves; unnamed threads are left out.
pub fn thread_names(pid: u32) -> Vec<String> {
    let mut names = Vec::new();
    // SAFETY: snapshot iteration with a sized entry; every handle is closed by OwnedHandle, and
    // the description buffer is freed with LocalFree as GetThreadDescription requires.
    unsafe {
        let snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPTHREAD, 0);
        if snapshot == INVALID_HANDLE_VALUE {
            return names;
        }
        let snapshot = OwnedHandle::from_raw_handle(snapshot);
        let mut entry: THREADENTRY32 = mem::zeroed();
        entry.dwSize = mem::size_of::<THREADENTRY32>() as u32;
        let mut more = Thread32First(snapshot.as_raw_handle(), &mut entry) != 0;
        while more {
            if entry.th32OwnerProcessID == pid {
                let thread = OpenThread(THREAD_QUERY_LIMITED_INFORMATION, 0, entry.th32ThreadID);
                if !thread.is_null() {
                    let thread = OwnedHandle::from_raw_handle(thread);
                    let mut text = ptr::null_mut();
                    if GetThreadDescription(thread.as_raw_handle(), &mut text) >= 0 && !text.is_null() {
                        let mut len = 0;
                        while *text.add(len) != 0 {
                            len += 1;
                        }
                        let name = String::from_utf16_lossy(std::slice::from_raw_parts(text, len));
                        if !name.is_empty() {
                            names.push(name);
                        }
                        LocalFree(text.cast());
                    }
                }
            }
            more = Thread32Next(snapshot.as_raw_handle(), &mut entry) != 0;
        }
    }
    names
}

/// The working set of process `pid` in bytes, if it can be read.
pub fn working_set(pid: u32) -> Option<usize> {
    // SAFETY: the handle is checked and closed by OwnedHandle; the counters are plain data.
    unsafe {
        let process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_VM_READ, 0, pid);
        if process.is_null() {
            return None;
        }
        let process = OwnedHandle::from_raw_handle(process);
        let mut memory: PROCESS_MEMORY_COUNTERS = mem::zeroed();
        memory.cb = mem::size_of::<PROCESS_MEMORY_COUNTERS>() as u32;
        (K32GetProcessMemoryInfo(process.as_raw_handle(), &mut memory, memory.cb) != 0)
            .then_some(memory.WorkingSetSize)
    }
}
