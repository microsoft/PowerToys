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
use std::path::PathBuf;
use std::time::{Duration, Instant};
use std::{fs, ptr};

use support::*;
use windows_sys::Win32::System::LibraryLoader::{GetProcAddress, LoadLibraryW};
use windows_sys::Win32::System::Threading::{OpenProcess, PROCESS_SYNCHRONIZE, WaitForSingleObject};
use windows_sys::Win32::UI::WindowsAndMessaging::GetWindowThreadProcessId;

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

fn repo_output_dir() -> PathBuf {
    PathBuf::from(env!("CARGO_MANIFEST_DIR")).join("../../../../../x64/Debug")
}

#[test]
#[ignore = "uses the real PowerToys data folder and kernel object names"]
fn the_runner_contract_still_holds() {
    let built_dll = repo_output_dir().join("PowerToys.AwakeModuleInterface.dll");
    assert!(
        built_dll.exists(),
        "build AwakeModuleInterface (Debug x64) first: {}",
        built_dll.display()
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
    let window = find_awake_window(Duration::from_secs(10)).expect("Awake started and created its window");
    let mut pid = 0;
    // SAFETY: valid window handle.
    unsafe { GetWindowThreadProcessId(window, &mut pid) };

    // The Settings app reaches Awake through set_config, which writes settings.json.
    module.set_config(r#"{"name":"Awake","version":"1.0","properties":{"keepDisplayOn":false,"mode":1,"intervalHours":0,"intervalMinutes":1,"expirationDateTime":"2026-01-01T00:00:00+00:00","customTrayTimes":{}}}"#);
    assert!(
        wait_for_real_log("Entered Indefinite.", Duration::from_secs(10)),
        "Awake applied the runner's settings"
    );

    let started = Instant::now();
    module.call(DISABLE);
    assert!(!module.is_enabled());
    // SAFETY: querying a pid for SYNCHRONIZE; a null handle means it is already gone.
    let gone = unsafe {
        let process = OpenProcess(PROCESS_SYNCHRONIZE, 0, pid);
        process.is_null() || WaitForSingleObject(process, 0) == 0
    };
    assert!(gone, "disable() returns only after Awake exited");
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

fn find_awake_window(timeout: Duration) -> Option<windows_sys::Win32::Foundation::HWND> {
    let class = wide("Awake.MessageWindow");
    let deadline = Instant::now() + timeout;
    while Instant::now() < deadline {
        // SAFETY: looking up a top-level window by class.
        let window = unsafe {
            windows_sys::Win32::UI::WindowsAndMessaging::FindWindowExW(
                ptr::null_mut(),
                ptr::null_mut(),
                class.as_ptr(),
                ptr::null(),
            )
        };
        if !window.is_null() {
            return Some(window);
        }
        std::thread::sleep(Duration::from_millis(50));
    }
    None
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
