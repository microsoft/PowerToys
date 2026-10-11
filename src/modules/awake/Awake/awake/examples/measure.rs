//! Measures an Awake build the same way for any implementation:
//!
//! ```text
//! cargo run --release --example measure -- <label> <exe> <args...>
//! ```
//!
//! `{pid}` in the arguments is replaced with this process's id, so both implementations can be
//! bound to the measuring process. Each run starts the program exactly as the PowerToys runner
//! starts Awake. Reports startup time until the tray window exists, the footprint after
//! settling, CPU time used while idle, and any console host that runs alongside, whose memory
//! the program's own numbers leave out.

#[path = "../tests/support/runner.rs"]
mod runner;

use std::ffi::OsString;
use std::time::{Duration, Instant};
use std::{mem, ptr};

use runner::Launched;

use windows_sys::Win32::Foundation::{FILETIME, HWND};
use windows_sys::Win32::System::Diagnostics::ToolHelp::{
    CreateToolhelp32Snapshot, PROCESSENTRY32W, Process32FirstW, Process32NextW, TH32CS_SNAPPROCESS,
};
use windows_sys::Win32::System::ProcessStatus::{K32GetProcessMemoryInfo, PROCESS_MEMORY_COUNTERS_EX};
use windows_sys::Win32::System::Threading::{
    DETACHED_PROCESS, GR_GDIOBJECTS, GR_USEROBJECTS, GetGuiResources, GetProcessHandleCount, GetProcessTimes,
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
        let (child, startup) = start(&exe, &rest);
        startups.push(startup);
        if run + 1 < RUNS {
            child.kill();
        } else {
            last = Some(child);
        }
        std::thread::sleep(Duration::from_millis(300));
    }
    startups.sort();
    let child = last.expect("last run");

    std::thread::sleep(SETTLE);
    let cpu_before = cpu_time(&child);
    let cycles_before = cycles(&child);
    std::thread::sleep(IDLE_WINDOW);
    let cpu_after = cpu_time(&child);
    let cycles_after = cycles(&child);
    let footprint = footprint(&child);
    let hosts = runner::console_hosts(child.pid);
    let host_memory: usize = hosts.iter().filter_map(|&host| runner::working_set(host)).sum();
    child.kill();

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
    if !hosts.is_empty() {
        println!(
            "{label}: {} console host(s) alongside, working set {:.1} MB",
            hosts.len(),
            scaled(host_memory as u64, 1_048_576.0),
        );
    }
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
fn start(exe: &str, args: &[String]) -> (Launched, Duration) {
    let started = Instant::now();
    // Keep logs and settings out of the real profile; harmless for builds that ignore it.
    let data_dir = std::env::temp_dir().join(format!("awake-measure-{}", std::process::id()));
    let env = [
        ("POWERTOYS_AWAKE_DATA_DIR", data_dir.into_os_string()),
        ("POWERTOYS_AWAKE_OBJECT_SUFFIX", OsString::from("-measure")),
    ];
    let args: Vec<&str> = args.iter().map(String::as_str).collect();
    // The runner's flags; the launcher passes no standard handles, as the runner does not.
    let child = runner::launch(exe, &args, &env, DETACHED_PROCESS).expect("start");
    let class: Vec<u16> = "Awake.MessageWindow".encode_utf16().chain(Some(0)).collect();
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
            if owner == child.pid {
                return (child, started.elapsed());
            }
        }
        // Dropping the child ends it.
        assert!(
            started.elapsed() <= Duration::from_secs(30),
            "no window from {exe}"
        );
        std::thread::sleep(Duration::from_millis(1));
    }
}

fn cpu_time(child: &Launched) -> u64 {
    let mut times = [FILETIME {
        dwLowDateTime: 0,
        dwHighDateTime: 0,
    }; 4];
    let [created, exited, kernel, user] = &mut times;
    // SAFETY: valid process handle and out-pointers.
    unsafe { GetProcessTimes(child.handle(), created, exited, kernel, user) };
    let ticks = |t: &FILETIME| (u64::from(t.dwHighDateTime) << 32) | u64::from(t.dwLowDateTime);
    ticks(kernel) + ticks(user)
}

/// Exact CPU cycles used by all threads, unlike the 15.6 ms granularity of process times.
fn cycles(child: &Launched) -> u64 {
    let mut cycles = 0;
    // SAFETY: valid process handle and out-pointer.
    unsafe { QueryProcessCycleTime(child.handle(), &mut cycles) };
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

fn footprint(child: &Launched) -> Footprint {
    let process = child.handle();
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
            threads: threads(child.pid),
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
