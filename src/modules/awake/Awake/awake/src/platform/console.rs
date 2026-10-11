//! The terminal Awake was started from, if any.
//!
//! Output goes through a dedicated thread: writing to a console can block (for example while the
//! user selects text in it), and that must never stall the window thread that holds the
//! keep-awake request.

use std::os::windows::io::{FromRawHandle, IntoRawHandle, OwnedHandle};
use std::sync::atomic::{AtomicPtr, AtomicU64, Ordering};
use std::sync::mpsc::{Receiver, SyncSender, TrySendError, sync_channel};
use std::thread::JoinHandle;
use std::time::{Duration, Instant};
use std::{io, mem, ptr};

use awake_core::text::Style;
use windows_sys::Win32::Foundation::{HANDLE, HWND, INVALID_HANDLE_VALUE, WAIT_TIMEOUT};
use windows_sys::Win32::Storage::FileSystem::WriteFile;
use windows_sys::Win32::System::Console::{
    CTRL_BREAK_EVENT, CTRL_C_EVENT, CTRL_CLOSE_EVENT, CTRL_LOGOFF_EVENT, CTRL_SHUTDOWN_EVENT,
    ENABLE_PROCESSED_OUTPUT, ENABLE_VIRTUAL_TERMINAL_PROCESSING, FreeConsole, GetConsoleMode,
    GetConsoleProcessList, GetConsoleWindow, GetStdHandle, STD_ERROR_HANDLE, STD_OUTPUT_HANDLE,
    SetConsoleCtrlHandler, SetConsoleMode, WriteConsoleW,
};
use windows_sys::Win32::System::Threading::{
    CREATE_NEW_CONSOLE, CREATE_NO_WINDOW, CreateProcessW, PROCESS_INFORMATION, STARTUPINFOW, SetEvent,
    WaitForSingleObject,
};
use windows_sys::Win32::UI::WindowsAndMessaging::PostMessageW;

use super::last_error;
use super::messages::WM_CONSOLE_CTRL;

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Output {
    /// An interactive console window.
    Console(Style),
    /// A pipe or a file: plain UTF-8, no live updates.
    Redirected,
    /// No standard output at all.
    None,
}

/// The output mode the console had before `detect` changed it, or `NOT_SAVED`.
static ORIGINAL_MODE: AtomicU64 = AtomicU64::new(NOT_SAVED);
const NOT_SAVED: u64 = u64::MAX;

/// Inspects standard output and, for a console, turns on escape sequences. The mode belongs to
/// the screen buffer the calling shell keeps using, so call [`restore_mode`] before exiting.
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
    let wanted = mode | ENABLE_PROCESSED_OUTPUT | ENABLE_VIRTUAL_TERMINAL_PROCESSING;
    // SAFETY: as above.
    let vt = unsafe { SetConsoleMode(handle, wanted) } != 0;
    if vt && wanted != mode {
        // Only the first change is remembered, so a second call cannot save Awake's own mode.
        let _ =
            ORIGINAL_MODE.compare_exchange(NOT_SAVED, u64::from(mode), Ordering::AcqRel, Ordering::Acquire);
    }
    let color = vt && std::env::var_os("NO_COLOR").is_none_or(|v| v.is_empty());
    // Windows Terminal and VS Code ship fonts with the symbols the rich output uses.
    let rich_glyphs = std::env::var_os("WT_SESSION").is_some() || std::env::var_os("TERM_PROGRAM").is_some();
    Output::Console(Style {
        vt,
        color,
        rich_glyphs,
    })
}

/// Gives the console back the output mode it had before [`detect`], so the shell does not keep
/// interpreting escape sequences after Awake exits. Does nothing when the mode was not changed,
/// and only the first call restores.
pub fn restore_mode() {
    let saved = ORIGINAL_MODE.swap(NOT_SAVED, Ordering::AcqRel);
    let handle = std_handle(STD_OUTPUT_HANDLE);
    if saved == NOT_SAVED || handle.is_null() || handle == INVALID_HANDLE_VALUE {
        return;
    }
    // SAFETY: valid handle; `saved` came from GetConsoleMode on this same buffer, so it fits in
    // a u32. A failure (the console is already gone) leaves nothing to restore.
    unsafe { SetConsoleMode(handle, saved as u32) };
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
        for chunk in console_chunks(&units, CONSOLE_CHUNK) {
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
        // Styles follow standard output, so text for a redirected standard error can still
        // carry escape sequences; a file or pipe gets them stripped.
        let text = strip_escapes(text);
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

/// Units per WriteConsoleW call, which keeps each call's buffer small.
const CONSOLE_CHUNK: usize = 8192;

/// Splits UTF-16 text into pieces of at most `max` units without separating a surrogate pair,
/// which the console would otherwise show as two replacement characters.
fn console_chunks(units: &[u16], max: usize) -> impl Iterator<Item = &[u16]> {
    debug_assert!(max >= 2, "a chunk must fit a surrogate pair");
    let mut rest = units;
    std::iter::from_fn(move || {
        if rest.is_empty() {
            return None;
        }
        let mut end = rest.len().min(max);
        if end < rest.len() && (0xD800..=0xDBFF).contains(&rest[end - 1]) {
            end -= 1;
        }
        let (chunk, tail) = rest.split_at(end);
        rest = tail;
        Some(chunk)
    })
}

/// Removes the CSI escape sequences (`ESC [ ... final`) the console styles produce.
fn strip_escapes(text: &str) -> std::borrow::Cow<'_, str> {
    if !text.contains('\x1b') {
        return text.into();
    }
    let mut plain = String::with_capacity(text.len());
    let mut chars = text.chars();
    while let Some(c) = chars.next() {
        if c != '\x1b' {
            plain.push(c);
            continue;
        }
        if chars.clone().next() == Some('[') {
            chars.next();
            // Parameters and intermediates run until a final byte in '@'..='~'.
            for c in chars.by_ref() {
                if ('@'..='~').contains(&c) {
                    break;
                }
            }
        }
    }
    plain.into()
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
    // Intentionally never closed: the handler may wait on it until the process ends.
    if let Ok(done) = super::event::manual_reset(None) {
        SHUTDOWN_DONE.store(done.into_raw_handle(), Ordering::Release);
    }
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
    restore_mode();
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
    fn console_chunks_keep_surrogate_pairs_whole() {
        let units: Vec<u16> = "ab😀cd".encode_utf16().collect();
        let chunks: Vec<&[u16]> = console_chunks(&units, 3).collect();
        // The emoji would start at unit 2 of the first chunk, so it moves to the next one.
        assert_eq!(chunks, [&units[..2], &units[2..5], &units[5..]]);
        for chunk in &chunks {
            assert!(String::from_utf16(chunk).is_ok(), "no chunk holds half a pair");
        }
        assert_eq!(console_chunks(&units, 8192).count(), 1);
        assert_eq!(console_chunks(&[], 8).count(), 0);
        let long: Vec<u16> = "x".repeat(10).encode_utf16().collect();
        assert_eq!(
            console_chunks(&long, 4).map(<[u16]>::len).collect::<Vec<_>>(),
            [4, 4, 2]
        );
    }

    #[test]
    fn escapes_are_stripped_for_files() {
        assert_eq!(strip_escapes("plain"), "plain");
        assert_eq!(strip_escapes("\x1b[31mError:\x1b[0m bad"), "Error: bad");
        assert_eq!(strip_escapes("\r\x1b[2K\x1b[1;32m●\x1b[0m done"), "\r● done");
        // A lone ESC is dropped rather than written as a control character.
        assert_eq!(strip_escapes("a\x1bb"), "ab");
        assert_eq!(strip_escapes("tail\x1b["), "tail");
    }

    #[test]
    fn quoting_round_trips_through_the_windows_rules() {
        assert_eq!(quote("for"), "for");
        assert_eq!(quote("C:\\Program Files\\x.exe"), "\"C:\\Program Files\\x.exe\"");
        assert_eq!(quote(""), "\"\"");
        assert_eq!(quote("a\"b"), "\"a\\\"b\"");
        assert_eq!(quote("dir\\ end\\"), "\"dir\\ end\\\\\"");
    }
}
