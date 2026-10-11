//! Runs Awake inside a Windows job object that caps its committed memory, to check that a
//! modest budget is plenty. A second test is the control: it shows the cap is really applied,
//! so the budget test cannot pass because the limit silently failed to take effect.

mod support;

use std::os::windows::io::{AsRawHandle, FromRawHandle, OwnedHandle};
use std::time::Duration;
use std::{mem, ptr};

use awake_testkit::runner;
use support::*;
use windows_sys::Win32::System::JobObjects::{
    AssignProcessToJobObject, CreateJobObjectW, JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE,
    JOB_OBJECT_LIMIT_PROCESS_MEMORY, JOBOBJECT_EXTENDED_LIMIT_INFORMATION, JobObjectExtendedLimitInformation,
    SetInformationJobObject,
};
use windows_sys::Win32::System::Threading::{CREATE_NO_WINDOW, CREATE_SUSPENDED};

/// Starts `awake for 1s` under a per-process commit limit and returns its exit code, or `None`
/// if it was still running after `timeout`.
fn run_with_memory_limit(sandbox: &Sandbox, limit_bytes: usize, timeout: Duration) -> Option<u32> {
    // SAFETY: null attributes and name make a private job.
    let job = unsafe { CreateJobObjectW(ptr::null(), ptr::null()) };
    assert!(!job.is_null());
    // SAFETY: a fresh handle we own; closing it ends whatever still runs in the job.
    let job = unsafe { OwnedHandle::from_raw_handle(job) };
    // SAFETY: plain data, for which all zeroes is a valid value.
    let mut limits: JOBOBJECT_EXTENDED_LIMIT_INFORMATION = unsafe { mem::zeroed() };
    limits.BasicLimitInformation.LimitFlags =
        JOB_OBJECT_LIMIT_PROCESS_MEMORY | JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;
    limits.ProcessMemoryLimit = limit_bytes;
    // SAFETY: valid job handle and a limits structure of the size given.
    let limited = unsafe {
        SetInformationJobObject(
            job.as_raw_handle(),
            JobObjectExtendedLimitInformation,
            (&raw const limits).cast(),
            mem::size_of_val(&limits) as u32,
        )
    };
    assert_ne!(limited, 0);

    // Suspended, so the limit applies before Awake runs a single instruction.
    let awake = runner::launch(
        EXE,
        &["for", "1s"],
        &sandbox.environment(),
        CREATE_SUSPENDED | CREATE_NO_WINDOW,
    )
    .expect("start awake");
    // SAFETY: valid job and process handles.
    let assigned = unsafe { AssignProcessToJobObject(job.as_raw_handle(), awake.handle()) };
    assert_ne!(assigned, 0);
    awake.resume();
    awake.wait_exit(timeout).map(|code| code as u32)
}

#[test]
fn a_modest_memory_budget_is_enough() {
    let sandbox = Sandbox::new("budget");
    let exit_code = run_with_memory_limit(&sandbox, 16 * 1024 * 1024, Duration::from_secs(15));
    assert_eq!(
        exit_code,
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
    let code = run_with_memory_limit(&sandbox, 512 * 1024, Duration::from_secs(15))
        .expect("the process ended on its own instead of hanging");
    assert!(
        code >= 0xC000_0000,
        "the system stopped the process with an error status, not Awake: {code:#x}"
    );
    assert!(sandbox.log().is_empty(), "Awake never got as far as logging");
}
