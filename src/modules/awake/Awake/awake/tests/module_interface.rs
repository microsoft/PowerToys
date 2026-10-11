//! Drives the real `PowerToys.AwakeModuleInterface.dll` the way the runner does: create it,
//! enable it (which launches this build of Awake), push settings through `set_config`, and
//! disable it (which must stop Awake cleanly, without `TerminateProcess`).
//!
//! Ignored by default because the DLL always logs to the real `%LOCALAPPDATA%\Microsoft\PowerToys`
//! and talks to Awake through the real kernel object names. CI runs it from Awake.vcxproj's Test
//! target; locally, build the DLL and run
//! `cargo test -p awake --test module_interface -- --ignored`.

#![cfg(target_arch = "x86_64")]

mod support;

use std::ffi::c_void;
use std::fs;
use std::os::windows::io::{AsRawHandle, FromRawHandle, OwnedHandle};
use std::path::{Path, PathBuf};
use std::time::{Duration, Instant};

use awake_core::mode::Mode;
use awake_testkit::process::{any_awake_window, find_window, image_path, process_ids_named};
use awake_testkit::{log_lines, read_logs, wide};
use support::*;
use windows_sys::Win32::Foundation::WAIT_OBJECT_0;
use windows_sys::Win32::System::LibraryLoader::{GetProcAddress, LoadLibraryW};
use windows_sys::Win32::System::Threading::{
    OpenProcess, PROCESS_QUERY_LIMITED_INFORMATION, PROCESS_SYNCHRONIZE, PROCESS_TERMINATE, TerminateProcess,
    WaitForSingleObject,
};

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
            std::mem::transmute::<*const c_void, SetConfig>(self.slot(SET_CONFIG))(self.0, json.as_ptr());
        }
    }
}

/// The PowerToys output folder that holds the module DLL. Awake.vcxproj passes its `$(OutDir)`;
/// a plain `cargo test` uses the conventional `<repo>\x64\<configuration>` for its profile.
fn repo_output_dir() -> PathBuf {
    if let Some(out_dir) = std::env::var_os("POWERTOYS_AWAKE_MODULE_DIR") {
        return PathBuf::from(out_dir);
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
    // mutex and this build would refuse to start. Run right after the other end-to-end tests, the
    // last of their sandboxed instances may still be closing.
    assert!(
        no_awake_window_within(Duration::from_secs(10)),
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
        wait_for_real_log(&log_lines::entered(Mode::Indefinite), Duration::from_secs(10)),
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
        real_log().contains(log_lines::EXIT_EVENT_SIGNALED),
        "a clean exit, not TerminateProcess"
    );

    module.call(DESTROY);
    let _ = fs::remove_dir_all(&stage);
}

/// The Awake the module started, held open so its exit can be checked without a pid lookup, and
/// ended when dropped so a failed assertion never leaves it holding the machine awake.
struct StagedAwake {
    process: OwnedHandle,
}

impl StagedAwake {
    fn exited(&self) -> bool {
        // SAFETY: `process` is an open handle with SYNCHRONIZE access.
        unsafe { WaitForSingleObject(self.process.as_raw_handle(), 0) == WAIT_OBJECT_0 }
    }
}

impl Drop for StagedAwake {
    fn drop(&mut self) {
        if !self.exited() {
            // SAFETY: `process` is an open handle with PROCESS_TERMINATE access.
            unsafe { TerminateProcess(self.process.as_raw_handle(), 1) };
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
            // SAFETY: a fresh handle we own.
            let process = unsafe { OwnedHandle::from_raw_handle(process) };
            let ours = image_path(&process)
                .and_then(|path| fs::canonicalize(path).ok())
                .is_some_and(|path| path.as_os_str().eq_ignore_ascii_case(exe.as_os_str()));
            if ours && find_window(pid, Duration::from_millis(100)).is_some() {
                return Some(StagedAwake { process });
            }
        }
        std::thread::sleep(Duration::from_millis(50));
    }
    None
}

/// Whether every Awake tray window, whoever owns it, is gone within `timeout`.
fn no_awake_window_within(timeout: Duration) -> bool {
    let deadline = Instant::now() + timeout;
    loop {
        if !any_awake_window() {
            return true;
        }
        if Instant::now() >= deadline {
            return false;
        }
        std::thread::sleep(Duration::from_millis(100));
    }
}

fn real_log() -> String {
    let local = PathBuf::from(std::env::var("LOCALAPPDATA").unwrap());
    read_logs(&local.join(r"Microsoft\PowerToys\Awake\Logs"))
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
