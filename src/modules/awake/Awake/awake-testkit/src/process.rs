//! Finding Awake processes and their window, and measuring what they hold.

use std::os::windows::io::{AsHandle, AsRawHandle, FromRawHandle, OwnedHandle};
use std::path::PathBuf;
use std::time::{Duration, Instant};
use std::{mem, ptr};

use windows_sys::Win32::Foundation::{HWND, INVALID_HANDLE_VALUE, LocalFree};
use windows_sys::Win32::System::Diagnostics::ToolHelp::{
    CreateToolhelp32Snapshot, PROCESSENTRY32W, Process32FirstW, Process32NextW, TH32CS_SNAPPROCESS,
    TH32CS_SNAPTHREAD, THREADENTRY32, Thread32First, Thread32Next,
};
use windows_sys::Win32::System::ProcessStatus::{K32GetProcessMemoryInfo, PROCESS_MEMORY_COUNTERS_EX};
use windows_sys::Win32::System::Threading::{
    GR_GDIOBJECTS, GR_USEROBJECTS, GetGuiResources, GetProcessHandleCount, GetThreadDescription, OpenProcess,
    OpenThread, PROCESS_NAME_WIN32, PROCESS_QUERY_LIMITED_INFORMATION, PROCESS_VM_READ,
    QueryFullProcessImageNameW, THREAD_QUERY_LIMITED_INFORMATION,
};
use windows_sys::Win32::UI::WindowsAndMessaging::{FindWindowExW, GetWindowThreadProcessId};

use crate::wide;

/// The class of Awake's hidden tray window, the same for the .NET and the Rust Awake.
pub const WINDOW_CLASS: &str = "Awake.MessageWindow";

/// What a process holds at one moment.
#[derive(Clone, Copy, Debug)]
pub struct Footprint {
    /// Committed memory only this process uses.
    pub private_bytes: usize,
    /// Physical memory it occupies.
    pub working_set: usize,
    /// Open kernel handles.
    pub handles: u32,
    /// GDI objects.
    pub gdi_objects: u32,
    /// USER objects: windows, menus, icons.
    pub user_objects: u32,
    /// Threads, Windows' own thread-pool workers included.
    pub threads: u32,
}

/// Measures process `pid`, whose `process` handle has query and read access. Memory reads as 0
/// if Windows will not report it.
pub fn footprint(process: &impl AsHandle, pid: u32) -> Footprint {
    let memory = memory_counters(process).unwrap_or_default();
    let process = process.as_handle().as_raw_handle();
    // SAFETY: plain calls on a valid process handle.
    unsafe {
        let mut handles = 0;
        GetProcessHandleCount(process, &mut handles);
        Footprint {
            private_bytes: memory.PrivateUsage,
            working_set: memory.WorkingSetSize,
            handles,
            gdi_objects: GetGuiResources(process, GR_GDIOBJECTS),
            user_objects: GetGuiResources(process, GR_USEROBJECTS),
            threads: thread_count(pid),
        }
    }
}

/// The working set of process `pid` in bytes, if it can be opened and Windows reports it.
pub fn working_set(pid: u32) -> Option<usize> {
    // SAFETY: plain call; a null handle means the process is gone or inaccessible.
    let process = unsafe { OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_VM_READ, 0, pid) };
    if process.is_null() {
        return None;
    }
    // SAFETY: a fresh handle we own.
    let process = unsafe { OwnedHandle::from_raw_handle(process) };
    memory_counters(&process).map(|memory| memory.WorkingSetSize)
}

fn memory_counters(process: &impl AsHandle) -> Option<PROCESS_MEMORY_COUNTERS_EX> {
    // SAFETY: plain data sized for the call and a valid process handle.
    unsafe {
        let mut memory: PROCESS_MEMORY_COUNTERS_EX = mem::zeroed();
        memory.cb = mem::size_of::<PROCESS_MEMORY_COUNTERS_EX>() as u32;
        let ok = K32GetProcessMemoryInfo(
            process.as_handle().as_raw_handle(),
            (&raw mut memory).cast(),
            memory.cb,
        );
        (ok != 0).then_some(memory)
    }
}

/// Every process in a snapshot, with its ID first.
fn processes() -> Vec<(u32, PROCESSENTRY32W)> {
    let mut found = Vec::new();
    // SAFETY: snapshot iteration with a sized entry; OwnedHandle closes the snapshot.
    unsafe {
        let snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if snapshot == INVALID_HANDLE_VALUE {
            return found;
        }
        let snapshot = OwnedHandle::from_raw_handle(snapshot);
        let mut entry: PROCESSENTRY32W = mem::zeroed();
        entry.dwSize = mem::size_of::<PROCESSENTRY32W>() as u32;
        let mut more = Process32FirstW(snapshot.as_raw_handle(), &mut entry) != 0;
        while more {
            found.push((entry.th32ProcessID, entry));
            more = Process32NextW(snapshot.as_raw_handle(), &mut entry) != 0;
        }
    }
    found
}

/// The threads of process `pid`, or 0 if it is gone.
pub fn thread_count(pid: u32) -> u32 {
    processes()
        .into_iter()
        .find(|(id, _)| *id == pid)
        .map_or(0, |(_, entry)| entry.cntThreads)
}

/// The processes whose image is `name`, ignoring case.
pub fn process_ids_named(name: &str) -> Vec<u32> {
    processes()
        .into_iter()
        .filter(|(_, entry)| image_name_is(&entry.szExeFile, name))
        .map(|(pid, _)| pid)
        .collect()
}

/// The `conhost.exe` processes started for process `pid`, which Windows makes its children.
pub fn console_hosts(pid: u32) -> Vec<u32> {
    processes()
        .into_iter()
        .filter(|(_, entry)| {
            entry.th32ParentProcessID == pid && image_name_is(&entry.szExeFile, "conhost.exe")
        })
        .map(|(id, _)| id)
        .collect()
}

fn image_name_is(name: &[u16], expected: &str) -> bool {
    let end = name.iter().position(|&c| c == 0).unwrap_or(name.len());
    String::from_utf16_lossy(&name[..end]).eq_ignore_ascii_case(expected)
}

/// The full path of the image `process` runs.
pub fn image_path(process: &impl AsHandle) -> Option<PathBuf> {
    let process = process.as_handle().as_raw_handle();
    let mut buffer = [0u16; 1024];
    let mut len = buffer.len() as u32;
    // SAFETY: `buffer` holds `len` units; on success `len` is the length written.
    let ok =
        unsafe { QueryFullProcessImageNameW(process, PROCESS_NAME_WIN32, buffer.as_mut_ptr(), &mut len) };
    (ok != 0).then(|| PathBuf::from(String::from_utf16_lossy(&buffer[..len as usize])))
}

/// The tray window of process `pid`, if it has created one.
pub fn window_of(pid: u32) -> Option<HWND> {
    let class = wide(WINDOW_CLASS);
    let mut window: HWND = ptr::null_mut();
    loop {
        // SAFETY: iterating top-level windows by class name.
        window = unsafe { FindWindowExW(ptr::null_mut(), window, class.as_ptr(), ptr::null()) };
        if window.is_null() {
            return None;
        }
        let mut owner = 0;
        // SAFETY: valid window handle from FindWindowExW.
        unsafe { GetWindowThreadProcessId(window, &mut owner) };
        if owner == pid {
            return Some(window);
        }
    }
}

/// Waits up to `timeout` for the tray window of process `pid`.
pub fn find_window(pid: u32, timeout: Duration) -> Option<HWND> {
    let deadline = Instant::now() + timeout;
    loop {
        if let Some(window) = window_of(pid) {
            return Some(window);
        }
        if Instant::now() >= deadline {
            return None;
        }
        std::thread::sleep(Duration::from_millis(25));
    }
}

/// Whether any process has an Awake tray window.
pub fn any_awake_window() -> bool {
    let class = wide(WINDOW_CLASS);
    // SAFETY: looking up a top-level window by class.
    !unsafe { FindWindowExW(ptr::null_mut(), ptr::null_mut(), class.as_ptr(), ptr::null()) }.is_null()
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
