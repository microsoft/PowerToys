//! Drives the real `PowerToys.AwakeModuleInterface.dll` the way the runner does: create it,
//! enable it (which launches this build of Awake), push settings through `set_config`, and
//! disable it (which must stop Awake cleanly, without TerminateProcess).
//!
//! Ignored by default because the DLL always logs to the real `%LOCALAPPDATA%\Microsoft\PowerToys`
//! and talks to Awake through the real kernel object names. Run it with
//! `cargo test -p awake --test module_interface -- --ignored` after building the DLL.

#![cfg(target_arch = "x86_64")]

mod support;

use std::ffi::c_void;
use std::path::{Path, PathBuf};
use std::time::{Duration, Instant};
use std::{fs, mem, ptr};

use support::*;
use windows_sys::Win32::Foundation::{CloseHandle, HANDLE, HWND, WAIT_OBJECT_0};
use windows_sys::Win32::System::Diagnostics::ToolHelp::{
    CreateToolhelp32Snapshot, PROCESSENTRY32W, Process32FirstW, Process32NextW, TH32CS_SNAPPROCESS,
};
use windows_sys::Win32::System::LibraryLoader::{GetProcAddress, LoadLibraryW};
use windows_sys::Win32::System::Threading::{
    OpenProcess, PROCESS_NAME_WIN32, PROCESS_QUERY_LIMITED_INFORMATION, PROCESS_SYNCHRONIZE,
    PROCESS_TERMINATE, QueryFullProcessImageNameW, TerminateProcess, WaitForSingleObject,
};
use windows_sys::Win32::UI::WindowsAndMessaging::FindWindowExW;

/// Slots of `PowertoyModuleIface`, in declaration order (there is no virtual destructor).
const SET_CONFIG: usize = 3;
const ENABLE: usize = 5;
const DISABLE: usize = 6;
const IS_ENABLED: usize = 7;
const DESTROY: usize = 8;

type Method = unsafe extern "system" fn(*mut c_void);
type IsEnabled = unsafe extern "system" fn(*mut c_void) -> bool;
type SetConfig = unsafe extern "system" fn(*mut c_void, *const u16);

struct Module(*mut c_void);

impl Module {
    unsafe fn slot(&self, index: usize) -> *const c_void {
        // SAFETY: the object's first field is its vtable pointer (MSVC single inheritance).
        unsafe { *(*(self.0 as *const *const *const c_void)).add(index) }
    }

    fn call(&self, index: usize) {
        // SAFETY: the slot index matches a `void method()` in the interface.
        unsafe { std::mem::transmute::<*const c_void, Method>(self.slot(index))(self.0) }
    }

    fn is_enabled(&self) -> bool {
        // SAFETY: slot 7 is `bool is_enabled()`.
        unsafe { std::mem::transmute::<*const c_void, IsEnabled>(self.slot(IS_ENABLED))(self.0) }
    }

    fn set_config(&self, json: &str) {
        let json = wide(json);
        // SAFETY: slot 3 is `void set_config(const wchar_t*)`.
        unsafe {
            std::mem::transmute::<*const c_void, SetConfig>(self.slot(SET_CONFIG))(self.0, json.as_ptr())
        }
    }
}

/// The PowerToys output folder that matches this build. Through Awake.vcxproj, Cargo's target
/// folder is `<OutDir>\obj\Awake-test`, so the DLL built alongside is in `<OutDir>`. A plain
/// `cargo test` uses the conventional `<repo>\x64\<configuration>` for its profile.
fn repo_output_dir() -> PathBuf {
    let built_by_msbuild = Path::new(EXE).ancestors().find_map(|dir| {
        let target = dir.file_name()?.to_str()?;
        let obj = dir.parent()?;
        (target.starts_with("Awake") && obj.file_name()?.eq_ignore_ascii_case("obj")).then(|| obj.parent())?
    });
    if let Some(out_dir) = built_by_msbuild {
        return out_dir.to_path_buf();
    }
    let configuration = if cfg!(debug_assertions) {
        "Debug"
    } else {
        "Release"
    };
    PathBuf::from(env!("CARGO_MANIFEST_DIR"))
        .join("../../../../../x64")
        .join(configuration)
}

#[test]
#[ignore = "uses the real PowerToys data folder and kernel object names"]
fn the_runner_contract_still_holds() {
    let built_dll = repo_output_dir().join("PowerToys.AwakeModuleInterface.dll");
    assert!(
        built_dll.exists(),
        "build AwakeModuleInterface for this configuration first: {}",
        built_dll.display()
    );
    // The test uses the real kernel object names, so another Awake would win the single-instance
    // mutex and this build would refuse to start.
    assert!(
        any_awake_window().is_none(),
        "another Awake is running (an installed PowerToys?); exit it first"
    );

    // The DLL launches PowerToys.Awake.exe from its own folder, so stage both side by side.
    let stage = std::env::temp_dir().join(format!("awake-module-{}", std::process::id()));
    let _ = fs::remove_dir_all(&stage);
    fs::create_dir_all(&stage).unwrap();
    fs::copy(&built_dll, stage.join("PowerToys.AwakeModuleInterface.dll")).unwrap();
    fs::copy(EXE, stage.join("PowerToys.Awake.exe")).unwrap();

    let dll = wide(
        &stage
            .join("PowerToys.AwakeModuleInterface.dll")
            .display()
            .to_string(),
    );
    // SAFETY: loading the module and resolving its factory exactly as the runner does.
    let module = unsafe {
        let library = LoadLibraryW(dll.as_ptr());
        assert!(!library.is_null(), "load the module DLL");
        let create =
            GetProcAddress(library, c"powertoy_create".as_ptr().cast()).expect("powertoy_create export");
        let create: unsafe extern "C" fn() -> *mut c_void = std::mem::transmute(create);
        Module(create())
    };
    assert!(!module.0.is_null());
    assert!(!module.is_enabled());

    module.call(ENABLE);
    assert!(module.is_enabled(), "is_enabled() reports the user's choice");
    let awake = find_staged_awake(&stage.join("PowerToys.Awake.exe"), Duration::from_secs(10))
        .expect("Awake started from the staged folder and created its window");

    // The Settings app reaches Awake through set_config, which writes settings.json.
    module.set_config(r#"{"name":"Awake","version":"1.0","properties":{"keepDisplayOn":false,"mode":1,"intervalHours":0,"intervalMinutes":1,"expirationDateTime":"2026-01-01T00:00:00+00:00","customTrayTimes":{}}}"#);
    assert!(
        wait_for_real_log("Entered Indefinite.", Duration::from_secs(10)),
        "Awake applied the runner's settings"
    );

    let started = Instant::now();
    module.call(DISABLE);
    assert!(!module.is_enabled());
    assert!(awake.exited(), "disable() returns only after Awake exited");
    assert!(
        started.elapsed() < Duration::from_secs(3),
        "Awake exited on the signal, not on the timeout: {:?}",
        started.elapsed()
    );
    assert!(
        real_log().contains("Exit event signaled."),
        "a clean exit, not TerminateProcess"
    );

    module.call(DESTROY);
    let _ = fs::remove_dir_all(&stage);
}

/// The Awake the module started, held open so its exit can be checked without a pid lookup, and
/// ended when dropped so a failed assertion never leaves it holding the machine awake.
struct StagedAwake {
    process: HANDLE,
}

impl StagedAwake {
    fn exited(&self) -> bool {
        // SAFETY: `process` is an open handle with SYNCHRONIZE access.
        unsafe { WaitForSingleObject(self.process, 0) == WAIT_OBJECT_0 }
    }
}

impl Drop for StagedAwake {
    fn drop(&mut self) {
        // SAFETY: `process` is an open handle with PROCESS_TERMINATE access, closed exactly once.
        unsafe {
            if !self.exited() {
                TerminateProcess(self.process, 1);
            }
            CloseHandle(self.process);
        }
    }
}

/// Waits for the Awake running from `exe` to create its window. Matching the image path, not just
/// the window class, keeps the test from picking up any other Awake on the machine.
fn find_staged_awake(exe: &Path, timeout: Duration) -> Option<StagedAwake> {
    let exe = fs::canonicalize(exe).ok()?;
    let deadline = Instant::now() + timeout;
    while Instant::now() < deadline {
        for pid in process_ids_named("PowerToys.Awake.exe") {
            // SAFETY: plain call; a null handle means the process is gone or inaccessible.
            let process = unsafe {
                OpenProcess(
                    PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_SYNCHRONIZE | PROCESS_TERMINATE,
                    0,
                    pid,
                )
            };
            if process.is_null() {
                continue;
            }
            let ours = image_path(process)
                .and_then(|path| fs::canonicalize(path).ok())
                .is_some_and(|path| path.as_os_str().eq_ignore_ascii_case(exe.as_os_str()));
            if ours && find_window(pid, Duration::from_millis(100)).is_some() {
                return Some(StagedAwake { process });
            }
            // SAFETY: the handle opened above, not used afterwards.
            unsafe { CloseHandle(process) };
        }
        std::thread::sleep(Duration::from_millis(50));
    }
    None
}

fn process_ids_named(name: &str) -> Vec<u32> {
    let mut ids = Vec::new();
    // SAFETY: the snapshot handle is closed below; the entry is sized as the API requires.
    unsafe {
        let snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        let mut entry: PROCESSENTRY32W = mem::zeroed();
        entry.dwSize = mem::size_of::<PROCESSENTRY32W>() as u32;
        let mut more = Process32FirstW(snapshot, &mut entry) != 0;
        while more {
            let len = entry
                .szExeFile
                .iter()
                .position(|&c| c == 0)
                .unwrap_or(entry.szExeFile.len());
            if String::from_utf16_lossy(&entry.szExeFile[..len]).eq_ignore_ascii_case(name) {
                ids.push(entry.th32ProcessID);
            }
            more = Process32NextW(snapshot, &mut entry) != 0;
        }
        CloseHandle(snapshot);
    }
    ids
}

fn image_path(process: HANDLE) -> Option<PathBuf> {
    let mut buffer = [0u16; 1024];
    let mut len = buffer.len() as u32;
    // SAFETY: `buffer` holds `len` units; on success `len` is the length written.
    let ok =
        unsafe { QueryFullProcessImageNameW(process, PROCESS_NAME_WIN32, buffer.as_mut_ptr(), &mut len) };
    (ok != 0).then(|| PathBuf::from(String::from_utf16_lossy(&buffer[..len as usize])))
}

/// Any Awake tray window, whoever owns it.
fn any_awake_window() -> Option<HWND> {
    let class = wide("Awake.MessageWindow");
    // SAFETY: looking up a top-level window by class.
    let window = unsafe { FindWindowExW(ptr::null_mut(), ptr::null_mut(), class.as_ptr(), ptr::null()) };
    (!window.is_null()).then_some(window)
}

fn real_log() -> String {
    let logs = PathBuf::from(std::env::var("LOCALAPPDATA").unwrap()).join(r"Microsoft\PowerToys\Awake\Logs");
    let mut text = String::new();
    for version in fs::read_dir(&logs)
        .into_iter()
        .flatten()
        .flatten()
        .filter(|e| e.path().is_dir())
    {
        for file in fs::read_dir(version.path()).into_iter().flatten().flatten() {
            text.push_str(&fs::read_to_string(file.path()).unwrap_or_default());
        }
    }
    text
}

fn wait_for_real_log(needle: &str, timeout: Duration) -> bool {
    let deadline = Instant::now() + timeout;
    while Instant::now() < deadline {
        if real_log().contains(needle) {
            return true;
        }
        std::thread::sleep(Duration::from_millis(50));
    }
    false
}
