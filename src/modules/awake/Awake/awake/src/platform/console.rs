//! The terminal Awake was started from, if any.
//!
//! Output goes through a dedicated thread: writing to a console can block (for example while the
//! user selects text in it), and that must never stall the window thread that holds the
//! keep-awake request.

use std::os::windows::io::{FromRawHandle, OwnedHandle};
use std::sync::atomic::{AtomicPtr, Ordering};
use std::sync::mpsc::{Receiver, SyncSender, TrySendError, sync_channel};
use std::thread::JoinHandle;
use std::time::{Duration, Instant};
use std::{io, mem, ptr};

use awake_core::console::Style;
use windows_sys::Win32::Foundation::{HANDLE, HWND, INVALID_HANDLE_VALUE, WAIT_TIMEOUT};
use windows_sys::Win32::Storage::FileSystem::WriteFile;
use windows_sys::Win32::System::Console::{
    CTRL_BREAK_EVENT, CTRL_C_EVENT, CTRL_CLOSE_EVENT, CTRL_LOGOFF_EVENT, CTRL_SHUTDOWN_EVENT,
    ENABLE_PROCESSED_OUTPUT, ENABLE_VIRTUAL_TERMINAL_PROCESSING, FreeConsole, GetConsoleMode,
    GetConsoleProcessList, GetConsoleWindow, GetStdHandle, STD_ERROR_HANDLE, STD_OUTPUT_HANDLE,
    SetConsoleCtrlHandler, SetConsoleMode, WriteConsoleW,
};
use windows_sys::Win32::System::Threading::{
    CREATE_NEW_CONSOLE, CREATE_NO_WINDOW, CreateEventW, CreateProcessW, PROCESS_INFORMATION, STARTUPINFOW,
    SetEvent, WaitForSingleObject,
};
use windows_sys::Win32::UI::WindowsAndMessaging::{PostMessageW, WM_APP};

use super::last_error;

pub const WM_CONSOLE_CTRL: u32 = WM_APP + 3;

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Output {
    /// An interactive console window.
    Console(Style),
    /// A pipe or a file: plain UTF-8, no live updates.
    Redirected,
    /// No standard output at all.
    None,
}

pub fn detect() -> Output {
    let handle = std_handle(STD_OUTPUT_HANDLE);
    if handle.is_null() || handle == INVALID_HANDLE_VALUE {
        return Output::None;
    }
    let mut mode = 0;
    // SAFETY: valid handle and out-pointer.
    if unsafe { GetConsoleMode(handle, &mut mode) } == 0 {
        return Output::Redirected;
    }
    // SAFETY: as above.
    let vt = unsafe {
        SetConsoleMode(
            handle,
            mode | ENABLE_PROCESSED_OUTPUT | ENABLE_VIRTUAL_TERMINAL_PROCESSING,
        )
    } != 0;
    let color = vt && std::env::var_os("NO_COLOR").is_none_or(|v| v.is_empty());
    // Windows Terminal and VS Code ship fonts with the symbols the rich output uses.
    let rich_glyphs = std::env::var_os("WT_SESSION").is_some() || std::env::var_os("TERM_PROGRAM").is_some();
    Output::Console(Style {
        vt,
        color,
        rich_glyphs,
    })
}

/// The processes attached to this console, Awake included; empty without a console.
pub fn attached_processes() -> Vec<u32> {
    let mut ids = vec![0u32; 64];
    loop {
        // SAFETY: the buffer holds `ids.len()` entries.
        let count = unsafe { GetConsoleProcessList(ids.as_mut_ptr(), ids.len() as u32) } as usize;
        // A count larger than the buffer means nothing was stored; more processes may attach
        // before the next call, so leave some room.
        if count > ids.len() {
            ids.resize(count + 16, 0);
            continue;
        }
        ids.truncate(count);
        return ids;
    }
}

/// Whether this process has a console window, visible or not.
pub fn has_console() -> bool {
    // SAFETY: plain call.
    !unsafe { GetConsoleWindow() }.is_null()
}

pub fn write_stdout(text: &str) {
    write(std_handle(STD_OUTPUT_HANDLE), text);
}

pub fn write_stderr(text: &str) {
    write(std_handle(STD_ERROR_HANDLE), text);
}

fn std_handle(which: u32) -> HANDLE {
    // SAFETY: plain value call.
    unsafe { GetStdHandle(which) }
}

fn write(handle: HANDLE, text: &str) {
    if handle.is_null() || handle == INVALID_HANDLE_VALUE || text.is_empty() {
        return;
    }
    let mut mode = 0;
    // SAFETY: valid handle; consoles take UTF-16 so no code page can garble the text.
    if unsafe { GetConsoleMode(handle, &mut mode) } != 0 {
        let units: Vec<u16> = text.encode_utf16().collect();
        for chunk in units.chunks(8192) {
            let mut written = 0;
            // SAFETY: `chunk` is valid for `len` units.
            unsafe {
                WriteConsoleW(
                    handle,
                    chunk.as_ptr().cast(),
                    chunk.len() as u32,
                    &mut written,
                    ptr::null(),
                )
            };
        }
    } else {
        for chunk in text.as_bytes().chunks(64 * 1024) {
            let mut written = 0;
            // SAFETY: `chunk` is valid for `len` bytes.
            unsafe {
                WriteFile(
                    handle,
                    chunk.as_ptr(),
                    chunk.len() as u32,
                    &mut written,
                    ptr::null_mut(),
                )
            };
        }
    }
}

enum Line {
    /// Printed as is.
    Text(String),
    /// Replaces the previous status line; skipped when a newer one is already queued.
    Status(String),
}

/// The live session display.
#[derive(Debug)]
pub struct LiveOutput {
    sender: Option<SyncSender<Line>>,
    thread: Option<JoinHandle<()>>,
}

impl std::fmt::Debug for Line {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.write_str(match self {
            Line::Text(_) => "Text",
            Line::Status(_) => "Status",
        })
    }
}

impl LiveOutput {
    pub fn start() -> io::Result<LiveOutput> {
        let (sender, receiver) = sync_channel(16);
        let thread = std::thread::Builder::new()
            .name("awake-console".into())
            .spawn(move || render(receiver))?;
        Ok(LiveOutput {
            sender: Some(sender),
            thread: Some(thread),
        })
    }

    pub fn print(&self, text: String) {
        self.send(Line::Text(text));
    }

    pub fn status(&self, text: String) {
        self.send(Line::Status(text));
    }

    fn send(&self, line: Line) {
        if let Some(sender) = &self.sender {
            // A full queue means the console is blocked; dropping an update beats waiting.
            if let Err(TrySendError::Full(_)) = sender.try_send(line) {
                log::debug!("Console output is blocked; skipped an update.");
            }
        }
    }

    /// Prints the last line and waits briefly for the console to take it.
    pub fn finish(mut self, last: String, timeout: Duration) {
        self.send(Line::Text(last));
        self.sender = None;
        if let Some(thread) = self.thread.take() {
            let deadline = Instant::now() + timeout;
            while !thread.is_finished() && Instant::now() < deadline {
                std::thread::sleep(Duration::from_millis(10));
            }
            if thread.is_finished() {
                let _ = thread.join();
            }
        }
    }
}

fn render(receiver: Receiver<Line>) {
    while let Ok(mut line) = receiver.recv() {
        // Coalesce: only the newest queued status line is worth drawing.
        while let (Line::Status(_), Ok(next)) = (&line, receiver.try_recv()) {
            line = next;
        }
        match line {
            Line::Text(text) | Line::Status(text) => write_stdout(&text),
        }
    }
}

static CTRL_WINDOW: AtomicPtr<core::ffi::c_void> = AtomicPtr::new(ptr::null_mut());
static SHUTDOWN_DONE: AtomicPtr<core::ffi::c_void> = AtomicPtr::new(ptr::null_mut());

/// Routes Ctrl+C, closing the console window, sign-out and shutdown to the window thread.
pub fn install_ctrl_handler(window: HWND) {
    CTRL_WINDOW.store(window, Ordering::Release);
    // SAFETY: anonymous manual-reset event, intentionally never closed (process lifetime).
    let done = unsafe { CreateEventW(ptr::null(), 1, 0, ptr::null()) };
    SHUTDOWN_DONE.store(done, Ordering::Release);
    // SAFETY: the handler is a plain function valid for the process lifetime.
    unsafe { SetConsoleCtrlHandler(Some(ctrl_handler), 1) };
}

/// Lets a pending console close or shutdown proceed once Awake has cleaned up.
pub fn signal_shutdown_complete() {
    let done = SHUTDOWN_DONE.load(Ordering::Acquire);
    if !done.is_null() {
        // SAFETY: valid event created in `install_ctrl_handler`.
        unsafe { SetEvent(done) };
    }
}

unsafe extern "system" fn ctrl_handler(kind: u32) -> i32 {
    let window = CTRL_WINDOW.load(Ordering::Acquire);
    if window.is_null() {
        return 0;
    }
    // SAFETY: posting to a window owned by another thread is allowed.
    unsafe { PostMessageW(window, WM_CONSOLE_CTRL, kind as usize, 0) };
    match kind {
        // Returning TRUE keeps Windows from ending the process, so if the window thread is hung
        // nothing else would: end it here once the watchdog time has passed.
        CTRL_C_EVENT | CTRL_BREAK_EVENT => {
            let done = SHUTDOWN_DONE.load(Ordering::Acquire);
            let millis = super::waiter::watchdog().as_millis() as u32;
            // SAFETY: valid event handle.
            if !done.is_null() && unsafe { WaitForSingleObject(done, millis) } == WAIT_TIMEOUT {
                super::waiter::end_hung_process();
            }
            1
        }
        // Returning from these ends the process, so give the window thread time to clean up.
        CTRL_CLOSE_EVENT | CTRL_LOGOFF_EVENT | CTRL_SHUTDOWN_EVENT => {
            let done = SHUTDOWN_DONE.load(Ordering::Acquire);
            if !done.is_null() {
                // SAFETY: valid event handle.
                unsafe { WaitForSingleObject(done, 4000) };
            }
            1
        }
        _ => 0,
    }
}

/// Starts Awake again with `args`, detached from the calling console, and returns at once. The
/// original Awake options never blocked the shell that started them, and scripts rely on that.
/// `new_console` gives the new process a console window of its own, as unbound sessions had;
/// bound sessions run without one, as before.
pub fn hand_off(args: &[String], new_console: bool) -> io::Result<OwnedHandle> {
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
    let exe = super::wide::wide(exe);
    let flags = if new_console {
        CREATE_NEW_CONSOLE
    } else {
        CREATE_NO_WINDOW
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

/// Leaves a console Awake does not need: one created just for it when a program without a
/// console started it bound to a process or under PowerToys.
pub fn detach() {
    // SAFETY: plain call; afterwards this process has no console.
    unsafe { FreeConsole() };
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
}
