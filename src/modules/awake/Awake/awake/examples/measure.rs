//! Measures an Awake build the same way for any implementation:
//!
//! ```text
//! cargo run --release --example measure -- <label> <exe> <args...>
//! ```
//!
//! `{pid}` in the arguments is replaced with this process's id, so both implementations can be
//! bound to the measuring process. Reports startup time until the tray window exists, the
//! footprint after settling, and CPU time used while idle.

use std::os::windows::io::AsRawHandle;
use std::os::windows::process::CommandExt;
use std::process::{Child, Command, Stdio};
use std::time::{Duration, Instant};
use std::{mem, ptr};

use windows_sys::Win32::Foundation::{FILETIME, HWND};
use windows_sys::Win32::System::Diagnostics::ToolHelp::{
    CreateToolhelp32Snapshot, PROCESSENTRY32W, Process32FirstW, Process32NextW, TH32CS_SNAPPROCESS,
};
use windows_sys::Win32::System::ProcessStatus::{K32GetProcessMemoryInfo, PROCESS_MEMORY_COUNTERS_EX};
use windows_sys::Win32::System::Threading::{
    CREATE_NO_WINDOW, GR_GDIOBJECTS, GR_USEROBJECTS, GetGuiResources, GetProcessHandleCount, GetProcessTimes,
};
use windows_sys::Win32::System::WindowsProgramming::QueryProcessCycleTime;
use windows_sys::Win32::UI::WindowsAndMessaging::{FindWindowExW, GetWindowThreadProcessId};

const RUNS: usize = 5;
const SETTLE: Duration = Duration::from_secs(3);
const IDLE_WINDOW: Duration = Duration::from_secs(10);

fn main() {
    let mut args = std::env::args().skip(1);
    let label = args.next().expect("label");
    let exe = args.next().expect("exe");
    let pid = std::process::id().to_string();
    let rest: Vec<String> = args.map(|a| a.replace("{pid}", &pid)).collect();

    let mut startups = Vec::new();
    let mut last = None;
    for run in 0..RUNS {
        let (mut child, startup) = start(&exe, &rest);
        startups.push(startup);
        if run + 1 < RUNS {
            child.kill().ok();
            child.wait().ok();
        } else {
            last = Some(child);
        }
        std::thread::sleep(Duration::from_millis(300));
    }
    startups.sort();
    let mut child = last.expect("last run");

    std::thread::sleep(SETTLE);
    let cpu_before = cpu_time(&child);
    let cycles_before = cycles(&child);
    std::thread::sleep(IDLE_WINDOW);
    let cpu_after = cpu_time(&child);
    let cycles_after = cycles(&child);
    let footprint = footprint(&child);
    child.kill().ok();
    child.wait().ok();

    println!(
        "{label}: startup median {:.1} ms (min {:.1}, max {:.1}); private {:.1} MB; working set {:.1} MB; handles {}; threads {}; GDI {}; USER {}; idle CPU {:.2} ms and {:.1} M cycles over {}s",
        ms(startups[RUNS / 2]),
        ms(startups[0]),
        ms(startups[RUNS - 1]),
        scaled(footprint.private_bytes as u64, 1_048_576.0),
        scaled(footprint.working_set as u64, 1_048_576.0),
        footprint.handles,
        footprint.threads,
        footprint.gdi,
        footprint.user,
        scaled(cpu_after - cpu_before, 10_000.0),
        scaled(cycles_after - cycles_before, 1_000_000.0),
        IDLE_WINDOW.as_secs(),
    );
}

fn ms(duration: Duration) -> f64 {
    duration.as_secs_f64() * 1000.0
}

#[allow(
    clippy::cast_precision_loss,
    reason = "display only, and these counts stay far below 2^52, where f64 is exact"
)]
fn scaled(value: u64, unit: f64) -> f64 {
    value as f64 / unit
}

/// Starts the program and waits for its tray window, which both implementations create with
/// the class `Awake.MessageWindow` once they are ready.
#[allow(
    clippy::zombie_processes,
    reason = "the timeout path kills and waits before panicking"
)]
fn start(exe: &str, args: &[String]) -> (Child, Duration) {
    let started = Instant::now();
    // Keep logs and settings out of the real profile; harmless for builds that ignore it.
    let data_dir = std::env::temp_dir().join(format!("awake-measure-{}", std::process::id()));
    let mut child = Command::new(exe)
        .args(args)
        .env("POWERTOYS_AWAKE_DATA_DIR", &data_dir)
        .env("POWERTOYS_AWAKE_OBJECT_SUFFIX", "-measure")
        // Like the PowerToys runner: no console of its own and none shared with this harness.
        .creation_flags(CREATE_NO_WINDOW)
        .stdin(Stdio::null())
        .stdout(Stdio::null())
        .stderr(Stdio::null())
        .spawn()
        .expect("start");
    let class: Vec<u16> = "Awake.MessageWindow".encode_utf16().chain(Some(0)).collect();
    #[allow(
        clippy::zombie_processes,
        reason = "the timeout path kills and waits before panicking"
    )]
    loop {
        let mut window: HWND = ptr::null_mut();
        loop {
            // SAFETY: iterating top-level windows by class name.
            window = unsafe { FindWindowExW(ptr::null_mut(), window, class.as_ptr(), ptr::null()) };
            if window.is_null() {
                break;
            }
            let mut owner = 0;
            // SAFETY: valid window handle.
            unsafe { GetWindowThreadProcessId(window, &mut owner) };
            if owner == child.id() {
                return (child, started.elapsed());
            }
        }
        if started.elapsed() > Duration::from_secs(30) {
            child.kill().ok();
            child.wait().ok();
            panic!("no window from {exe}");
        }
        std::thread::sleep(Duration::from_millis(1));
    }
}

fn cpu_time(child: &Child) -> u64 {
    let mut times = [FILETIME {
        dwLowDateTime: 0,
        dwHighDateTime: 0,
    }; 4];
    let [created, exited, kernel, user] = &mut times;
    // SAFETY: valid process handle and out-pointers.
    unsafe { GetProcessTimes(child.as_raw_handle(), created, exited, kernel, user) };
    let ticks = |t: &FILETIME| (u64::from(t.dwHighDateTime) << 32) | u64::from(t.dwLowDateTime);
    ticks(kernel) + ticks(user)
}

/// Exact CPU cycles used by all threads, unlike the 15.6 ms granularity of process times.
fn cycles(child: &Child) -> u64 {
    let mut cycles = 0;
    // SAFETY: valid process handle and out-pointer.
    unsafe { QueryProcessCycleTime(child.as_raw_handle(), &mut cycles) };
    cycles
}

struct Footprint {
    private_bytes: usize,
    working_set: usize,
    handles: u32,
    threads: u32,
    gdi: u32,
    user: u32,
}

fn footprint(child: &Child) -> Footprint {
    let process = child.as_raw_handle();
    // SAFETY: plain data and a valid process handle we own.
    unsafe {
        let mut memory: PROCESS_MEMORY_COUNTERS_EX = mem::zeroed();
        memory.cb = mem::size_of::<PROCESS_MEMORY_COUNTERS_EX>() as u32;
        K32GetProcessMemoryInfo(process, (&raw mut memory).cast(), memory.cb);
        let mut handles = 0;
        GetProcessHandleCount(process, &mut handles);
        Footprint {
            private_bytes: memory.PrivateUsage,
            working_set: memory.WorkingSetSize,
            handles,
            threads: threads(child.id()),
            gdi: GetGuiResources(process, GR_GDIOBJECTS),
            user: GetGuiResources(process, GR_USEROBJECTS),
        }
    }
}

fn threads(pid: u32) -> u32 {
    // SAFETY: snapshot iteration with a sized entry; the handle is closed below.
    unsafe {
        let snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        let mut entry: PROCESSENTRY32W = mem::zeroed();
        entry.dwSize = mem::size_of::<PROCESSENTRY32W>() as u32;
        let mut more = Process32FirstW(snapshot, &mut entry) != 0;
        let mut count = 0;
        while more {
            if entry.th32ProcessID == pid {
                count = entry.cntThreads;
            }
            more = Process32NextW(snapshot, &mut entry) != 0;
        }
        windows_sys::Win32::Foundation::CloseHandle(snapshot);
        count
    }
}
