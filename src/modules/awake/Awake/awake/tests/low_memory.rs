//! Runs Awake inside a Windows job object that caps its committed memory, to check that a
//! modest budget is plenty. A second test is the control: it shows the cap is really applied,
//! so the budget test cannot pass because the limit silently failed to take effect.

mod support;

use std::time::Duration;
use std::{mem, ptr};

use support::*;
use windows_sys::Win32::Foundation::{CloseHandle, HANDLE, WAIT_OBJECT_0};
use windows_sys::Win32::System::JobObjects::{
    AssignProcessToJobObject, CreateJobObjectW, JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE,
    JOB_OBJECT_LIMIT_PROCESS_MEMORY, JOBOBJECT_EXTENDED_LIMIT_INFORMATION, JobObjectExtendedLimitInformation,
    SetInformationJobObject,
};
use windows_sys::Win32::System::Threading::{
    CREATE_NO_WINDOW, CREATE_SUSPENDED, CREATE_UNICODE_ENVIRONMENT, CreateProcessW, GetExitCodeProcess,
    PROCESS_INFORMATION, ResumeThread, STARTUPINFOW, TerminateProcess, WaitForSingleObject,
};

struct Outcome {
    exit_code: Option<u32>,
}

/// Starts `awake for 1s` under a per-process commit limit and waits up to `timeout`.
fn run_with_memory_limit(sandbox: &Sandbox, limit_bytes: usize, timeout: Duration) -> Outcome {
    // SAFETY: plain Win32 calls with locally owned buffers; every handle is closed before return.
    unsafe {
        let job = CreateJobObjectW(ptr::null(), ptr::null());
        assert!(!job.is_null());
        let mut limits: JOBOBJECT_EXTENDED_LIMIT_INFORMATION = mem::zeroed();
        limits.BasicLimitInformation.LimitFlags =
            JOB_OBJECT_LIMIT_PROCESS_MEMORY | JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;
        limits.ProcessMemoryLimit = limit_bytes;
        assert_ne!(
            SetInformationJobObject(
                job,
                JobObjectExtendedLimitInformation,
                (&raw const limits).cast(),
                mem::size_of_val(&limits) as u32
            ),
            0
        );

        let mut environment: Vec<u16> = Vec::new();
        for (key, value) in std::env::vars_os() {
            environment.extend(key.to_string_lossy().encode_utf16());
            environment.push(u16::from(b'='));
            environment.extend(value.to_string_lossy().encode_utf16());
            environment.push(0);
        }
        for (key, value) in [
            (
                "POWERTOYS_AWAKE_DATA_DIR",
                sandbox.root.to_string_lossy().into_owned(),
            ),
            ("POWERTOYS_AWAKE_OBJECT_SUFFIX", sandbox.object_suffix()),
        ] {
            environment.extend(format!("{key}={value}").encode_utf16());
            environment.push(0);
        }
        environment.push(0);

        let exe = wide(EXE);
        let mut command_line = wide(&format!("\"{EXE}\" for 1s"));
        let mut startup: STARTUPINFOW = mem::zeroed();
        startup.cb = mem::size_of::<STARTUPINFOW>() as u32;
        let mut process: PROCESS_INFORMATION = mem::zeroed();
        assert_ne!(
            CreateProcessW(
                exe.as_ptr(),
                command_line.as_mut_ptr(),
                ptr::null(),
                ptr::null(),
                0,
                CREATE_SUSPENDED | CREATE_NO_WINDOW | CREATE_UNICODE_ENVIRONMENT,
                environment.as_ptr().cast(),
                ptr::null(),
                &startup,
                &mut process,
            ),
            0
        );
        assert_ne!(AssignProcessToJobObject(job, process.hProcess), 0);
        ResumeThread(process.hThread);

        let finished = WaitForSingleObject(process.hProcess, timeout.as_millis() as u32) == WAIT_OBJECT_0;
        let mut code = 0;
        let exit_code = if finished && GetExitCodeProcess(process.hProcess, &mut code) != 0 {
            Some(code)
        } else {
            TerminateProcess(process.hProcess, 99);
            None
        };
        for handle in [process.hThread, process.hProcess, job as HANDLE] {
            CloseHandle(handle);
        }
        Outcome { exit_code }
    }
}

#[test]
fn a_modest_memory_budget_is_enough() {
    let sandbox = Sandbox::new("budget");
    let outcome = run_with_memory_limit(&sandbox, 16 * 1024 * 1024, Duration::from_secs(15));
    assert_eq!(
        outcome.exit_code,
        Some(0),
        "a 1-second session completes within 16 MB of committed memory"
    );
}

/// Far below what any process needs to load, so Windows ends Awake before its own code runs: an
/// NTSTATUS instead of one of Awake's exit codes, and no log. This proves the limit applies, not
/// how Awake copes with failed allocations; with a little more memory, whether Awake gets far
/// enough to fail varies from run to run, so no budget tests that reliably.
#[test]
fn the_memory_limit_is_enforced() {
    let sandbox = Sandbox::new("starved");
    let outcome = run_with_memory_limit(&sandbox, 512 * 1024, Duration::from_secs(15));
    let code = outcome
        .exit_code
        .expect("the process ended on its own instead of hanging");
    assert!(
        code >= 0xC000_0000,
        "the system stopped the process with an error status, not Awake: {code:#x}"
    );
    assert!(sandbox.log().is_empty(), "Awake never got as far as logging");
}
