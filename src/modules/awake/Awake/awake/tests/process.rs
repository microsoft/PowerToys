//! End-to-end tests of the real executable, each in its own sandbox so they never touch a real
//! PowerToys installation. They run one at a time because some read machine-wide power state.

mod support;

use std::fs;
use std::process::Command;
use std::sync::{Mutex, MutexGuard, PoisonError};
use std::time::{Duration, Instant};

use awake_core::mode::Mode;
use awake_core::settings::{ModeSetting, SettingsDocument};
use awake_core::time::FixedZone;
use support::runner::{console_hosts, thread_names};
use support::*;
use windows_sys::Win32::System::Console::GetConsoleProcessList;
use windows_sys::Win32::System::Threading::{CREATE_NO_WINDOW, DETACHED_PROCESS};

const SECOND: Duration = Duration::from_secs(1);
/// How long a command that should return promptly may take before the test fails instead of
/// hanging; generous for a loaded build agent.
const PROMPTLY: Duration = Duration::from_secs(15);

fn serial() -> MutexGuard<'static, ()> {
    static LOCK: Mutex<()> = Mutex::new(());
    LOCK.lock().unwrap_or_else(PoisonError::into_inner)
}

fn own_pid() -> String {
    std::process::id().to_string()
}

/// The hand-off tests start Awake in the test's console, as a shell would. Without a console Awake
/// cannot see that its caller waits, so it would stay in the foreground and the test would fail
/// for a reason unrelated to the product.
fn assert_running_in_a_console() {
    let mut pids = [0u32; 8];
    // SAFETY: the buffer holds `pids.len()` entries; a larger count is returned, not written.
    let attached = unsafe { GetConsoleProcessList(pids.as_mut_ptr(), pids.len() as u32) };
    assert!(
        attached > 0,
        "these tests need a console, as a shell provides; run them from a terminal"
    );
}

fn read_settings(sandbox: &Sandbox) -> awake_core::settings::AwakeSettings {
    let bytes = fs::read(sandbox.settings_path()).unwrap();
    SettingsDocument::parse(&bytes)
        .unwrap()
        .settings(&FixedZone(0))
        .unwrap()
}

#[test]
fn powertoys_mode_follows_the_settings_file() {
    let _serial = serial();
    let sandbox = Sandbox::new("follow");
    let mut awake = sandbox.spawn(&["--use-pt-config", "--pid", &own_pid()]);

    let deadline = Instant::now() + 5 * SECOND;
    while !sandbox.settings_path().exists() && Instant::now() < deadline {
        std::thread::sleep(Duration::from_millis(20));
    }
    assert_eq!(
        read_settings(&sandbox).mode,
        ModeSetting::Known(Mode::Passive),
        "a default file is created"
    );

    sandbox.write_settings(&settings_json(1, false, 0, 1, "2026-01-01T00:00:00+00:00"));
    assert!(
        sandbox.wait_for_log("Execution state set to 0x80000001.", 1, 5 * SECOND),
        "{}",
        sandbox.log()
    );
    assert_ne!(
        system_execution_state() & 0x1,
        0,
        "Windows reports the system as required"
    );

    sandbox.write_settings(&settings_json(2, false, 0, 30, "2026-01-01T00:00:00+00:00"));
    assert!(sandbox.wait_for_log("Entered Timed.", 1, 5 * SECOND));

    // Turning the display on during a countdown must not restart it.
    sandbox.write_settings(&settings_json(2, true, 0, 30, "2026-01-01T00:00:00+00:00"));
    assert!(sandbox.wait_for_log("Execution state set to 0x80000003.", 1, 5 * SECOND));
    std::thread::sleep(Duration::from_millis(300));
    assert_eq!(
        sandbox.log().matches("Entered Timed.").count(),
        1,
        "the countdown was not restarted"
    );

    // An expiration already in the past is moved five minutes ahead and saved back.
    sandbox.write_settings(&settings_json(3, true, 0, 30, "2020-01-01T00:00:00+00:00"));
    assert!(sandbox.wait_for_log("Entered Expirable.", 1, 5 * SECOND));
    assert!(sandbox.wait_for_log("Saved", 1, 5 * SECOND));
    let saved = read_settings(&sandbox);
    assert_eq!(saved.mode, ModeSetting::Known(Mode::Expirable));
    let expiration = saved.expiration.expect("expiration saved");
    let now = std::time::SystemTime::now()
        .duration_since(std::time::UNIX_EPOCH)
        .unwrap()
        .as_secs() as i64;
    let saved_unix = expiration.utc.ticks() / 10_000_000 - 11_644_473_600;
    assert!(
        (saved_unix - now - 300).abs() < 30,
        "about five minutes ahead: {}",
        saved_unix - now
    );

    sandbox.signal_exit();
    assert_eq!(wait_exit(&mut awake, 5 * SECOND), Some(0));
}

#[test]
fn exits_when_the_bound_process_ends() {
    let _serial = serial();
    let sandbox = Sandbox::new("bound");
    let mut target = Running::new(
        Command::new("cmd.exe")
            .args(["/c", "ping -n 60 127.0.0.1 >nul"])
            .spawn()
            .unwrap(),
    );
    let mut awake = sandbox.spawn(&["--pid", &target.id().to_string()]);
    assert!(
        sandbox.wait_for_log("Entered Indefinite.", 1, 5 * SECOND),
        "{}",
        sandbox.log()
    );

    target.kill().unwrap();
    target.wait().unwrap();
    assert_eq!(wait_exit(&mut awake, 5 * SECOND), Some(0));
    let output = awake.wait_with_output().unwrap();
    assert!(String::from_utf8_lossy(&output.stdout).contains("The watched process exited"));
}

#[test]
fn a_second_instance_is_refused() {
    let _serial = serial();
    let sandbox = Sandbox::new("single");
    let mut first = sandbox.spawn(&["for", "1m"]);
    assert!(sandbox.wait_for_log("Entered Timed.", 1, 5 * SECOND));

    let started = Instant::now();
    let second = output_within(&mut sandbox.command(&["for", "1m"]), PROMPTLY);
    assert_eq!(second.status.code(), Some(1));
    assert!(String::from_utf8_lossy(&second.stderr).contains("already running"));
    assert!(started.elapsed() < 3 * SECOND, "refused promptly");
    assert_eq!(
        first.try_wait().unwrap(),
        None,
        "the first instance keeps running"
    );

    sandbox.signal_exit();
    assert_eq!(wait_exit(&mut first, 5 * SECOND), Some(0));
}

#[test]
fn a_powertoys_start_waits_for_the_previous_instance() {
    let _serial = serial();
    let sandbox = Sandbox::new("handover");
    let mut first = sandbox.spawn(&["--use-pt-config", "--pid", &own_pid()]);
    let first_window = find_window(first.id(), 5 * SECOND).expect("the first instance starts");

    let refused = output_within(
        sandbox
            .command(&["--use-pt-config", "--pid", &own_pid()])
            .env("POWERTOYS_AWAKE_TEST_HANDOVER_MS", "500"),
        PROMPTLY,
    );
    assert_eq!(
        refused.status.code(),
        Some(1),
        "refused once the hand-over time is up"
    );

    let mut second = sandbox.spawn(&["--use-pt-config", "--pid", &own_pid()]);
    std::thread::sleep(SECOND);
    assert_eq!(second.try_wait().unwrap(), None, "waits for the first instance");
    close_window(first_window);
    assert_eq!(wait_exit(&mut first, 5 * SECOND), Some(0));
    let second_window = find_window(second.id(), 5 * SECOND).expect("the second instance takes over");
    assert_eq!(second.try_wait().unwrap(), None);

    close_window(second_window);
    assert_eq!(wait_exit(&mut second, 5 * SECOND), Some(0));
}

#[test]
fn the_watchdog_ends_a_hung_awake() {
    let _serial = serial();
    let hung = |sandbox: &Sandbox, args: &[&str]| {
        Running::new(
            sandbox
                .command(args)
                .env("POWERTOYS_AWAKE_TEST_HANG_ON_EXIT", "1")
                .env("POWERTOYS_AWAKE_TEST_WATCHDOG_MS", "1000")
                .spawn()
                .unwrap(),
        )
    };

    // Stopped by the runner's exit event.
    let sandbox = Sandbox::new("watchdog-exit");
    let mut awake = hung(&sandbox, &["for", "1m", "--pid", &own_pid()]);
    assert!(sandbox.wait_for_log("Entered Timed.", 1, 5 * SECOND));
    sandbox.signal_exit();
    assert_eq!(wait_exit(&mut awake, 5 * SECOND), Some(1), "{}", sandbox.log());
    assert!(sandbox.log().contains("ending the process"), "{}", sandbox.log());

    // A command-line session whose time is up.
    let sandbox = Sandbox::new("watchdog-deadline");
    let mut awake = hung(&sandbox, &["for", "2s", "--pid", &own_pid()]);
    assert_eq!(wait_exit(&mut awake, 8 * SECOND), Some(1), "{}", sandbox.log());
    assert!(sandbox.log().contains("ending the process"), "{}", sandbox.log());
}

#[test]
fn a_command_line_session_ends_on_time() {
    let _serial = serial();
    let sandbox = Sandbox::new("timed");
    let started = Instant::now();
    let output = output_within(&mut sandbox.command(&["for", "2s"]), PROMPTLY);
    let elapsed = started.elapsed();
    assert_eq!(output.status.code(), Some(0));
    assert!(
        elapsed >= Duration::from_millis(1900) && elapsed < 6 * SECOND,
        "{elapsed:?}"
    );
    assert!(String::from_utf8_lossy(&output.stdout).contains("Time's up"));
}

#[test]
fn invalid_requests_fail_fast_with_a_message() {
    let _serial = serial();
    let sandbox = Sandbox::new("invalid");
    for (args, needle) in [
        (vec!["until", "2000-01-01T00:00:00Z"], "already passed"),
        (vec!["--pid", "999999"], "No running process"),
        (vec!["for", "soon"], "isn't a duration"),
        (vec!["--bogus"], "unexpected argument"),
    ] {
        let output = output_within(&mut sandbox.command(&args), PROMPTLY);
        assert_eq!(output.status.code(), Some(1), "{args:?}");
        assert!(
            String::from_utf8_lossy(&output.stderr).contains(needle),
            "{args:?}: {}",
            String::from_utf8_lossy(&output.stderr)
        );
    }
}

#[test]
fn corrupt_settings_are_reported_and_left_alone() {
    let _serial = serial();
    let sandbox = Sandbox::new("corrupt");
    sandbox.write_settings(&settings_json(1, false, 0, 1, "2026-01-01T00:00:00+00:00"));
    let mut awake = sandbox.spawn(&["-c", "--pid", &own_pid()]);
    assert!(sandbox.wait_for_log("Entered Indefinite.", 1, 5 * SECOND));

    sandbox.write_settings("{\"name\":\"Awake\",\"properties\":{\"mode\":");
    assert!(
        sandbox.wait_for_log("settings file is unreadable", 1, 5 * SECOND),
        "{}",
        sandbox.log()
    );
    assert_eq!(
        fs::read_to_string(sandbox.settings_path()).unwrap(),
        "{\"name\":\"Awake\",\"properties\":{\"mode\":"
    );
    assert_eq!(
        sandbox.log().matches("Entered Passive.").count(),
        0,
        "the session was not dropped"
    );

    sandbox.write_settings(&settings_json(0, false, 0, 1, "2026-01-01T00:00:00+00:00"));
    assert!(sandbox.wait_for_log("Entered Passive.", 1, 5 * SECOND));
    sandbox.signal_exit();
    assert_eq!(wait_exit(&mut awake, 5 * SECOND), Some(0));
}

/// Started exactly as the runner starts it, Awake carries nothing the runner cannot see: no
/// console host and no thread drawing a live status into a console nobody reads. That holds for
/// `DETACHED_PROCESS`, which the runner and bound hand-offs use, and for `CREATE_NO_WINDOW`,
/// whose hidden console Awake has to leave.
#[test]
fn a_start_without_a_console_window_keeps_no_console() {
    let _serial = serial();
    // The probe itself: a hidden console's host shows up as a child of the process it serves.
    let cmd = std::env::var("ComSpec").unwrap_or_else(|_| r"C:\Windows\System32\cmd.exe".into());
    let probe = support::runner::launch(&cmd, &["/c", "ping -n 5 127.0.0.1"], &[], CREATE_NO_WINDOW).unwrap();
    assert!(
        wait_until(5 * SECOND, || !console_hosts(probe.pid).is_empty()),
        "a hidden console's host is visible"
    );
    drop(probe);

    let pid = own_pid();
    for (name, flags) in [("detached", DETACHED_PROCESS), ("no-window", CREATE_NO_WINDOW)] {
        for (session, args) in [
            ("runner", ["--use-pt-config", "--pid", pid.as_str()].as_slice()),
            ("bound", ["for", "1m", "--pid", pid.as_str()].as_slice()),
        ] {
            let sandbox = Sandbox::new(&format!("console-{name}-{session}"));
            let awake = sandbox.launch_like_runner(args, flags);
            find_window(awake.pid, 5 * SECOND).expect("the session starts");
            assert!(
                wait_until(3 * SECOND, || console_hosts(awake.pid).is_empty()),
                "{name} {session}: a console host stayed with Awake"
            );
            let threads = thread_names(awake.pid);
            assert!(
                !threads.iter().any(|thread| thread == "awake-console"),
                "{name} {session}: live output into a console nobody sees: {threads:?}"
            );
            sandbox.signal_exit();
            assert_eq!(awake.wait_exit(5 * SECOND), Some(0), "{}", sandbox.log());
        }
    }
}

fn wait_until(timeout: Duration, mut done: impl FnMut() -> bool) -> bool {
    let deadline = Instant::now() + timeout;
    while Instant::now() < deadline {
        if done() {
            return true;
        }
        std::thread::sleep(Duration::from_millis(50));
    }
    done()
}

#[test]
fn closing_the_window_exits_instead_of_running_headless() {
    let _serial = serial();
    let sandbox = Sandbox::new("close");
    let mut awake = sandbox.spawn(&["-c", "--pid", &own_pid()]);
    let window = find_window(awake.id(), 5 * SECOND).expect("tray window");
    close_window(window);
    assert_eq!(wait_exit(&mut awake, 5 * SECOND), Some(0));
}

#[test]
fn a_restart_manager_close_ends_awake_instead_of_leaving_it_headless() {
    let _serial = serial();
    let sandbox = Sandbox::new("restart-manager");
    let mut awake = sandbox.spawn(&["-c", "--pid", &own_pid()]);
    let window = find_window(awake.id(), 5 * SECOND).expect("tray window");
    end_session_for_restart_manager(window);
    assert_eq!(wait_exit(&mut awake, 5 * SECOND), Some(0));
    assert!(sandbox.log().contains("Asked to close so files can be updated."));
    assert!(
        sandbox.log().contains("Exiting with code 0."),
        "the last line is written before the process ends"
    );
}

#[test]
fn failing_writes_degrade_gracefully() {
    let _serial = serial();
    let sandbox = Sandbox::new("readonly");
    // A read-only settings file stands in for a disk that refuses writes.
    sandbox.write_settings(&settings_json(3, false, 0, 1, "2020-01-01T00:00:00+00:00"));
    let path = sandbox.settings_path();
    let mut permissions = fs::metadata(&path).unwrap().permissions();
    permissions.set_readonly(true);
    fs::set_permissions(&path, permissions.clone()).unwrap();

    let mut awake = sandbox.spawn(&["-c", "--pid", &own_pid()]);
    let window = find_window(awake.id(), 5 * SECOND).expect("tray window");
    assert!(sandbox.wait_for_log("Entered Expirable.", 1, 5 * SECOND));
    assert!(
        sandbox.wait_for_log("Could not save settings", 1, 10 * SECOND),
        "{}",
        sandbox.log()
    );
    assert!(
        responsive(window, 2 * SECOND),
        "the window thread never blocked on the failing writes"
    );
    assert_eq!(awake.try_wait().unwrap(), None);

    // Clears the read-only attribute on Windows (the lint is about Unix permission bits).
    #[allow(clippy::permissions_set_readonly_false)]
    permissions.set_readonly(false);
    fs::set_permissions(&path, permissions).unwrap();
    sandbox.signal_exit();
    assert_eq!(wait_exit(&mut awake, 5 * SECOND), Some(0));
}

/// Rapid mode switches through the settings file, as a user clicking through the Settings app
/// would cause. It takes about half a minute, which is acceptable in the default run: it is the
/// only test of the reload path under load and of resource leaks, and its checks are relative to
/// the process itself, so a slow machine makes it slower rather than red.
#[test]
fn mode_switches_do_not_leak_or_hang() {
    let _serial = serial();
    let sandbox = Sandbox::new("soak");
    let mut awake = sandbox.spawn(&["-c", "--pid", &own_pid()]);
    let window = find_window(awake.id(), 5 * SECOND).expect("tray window");
    let future = "2099-01-01T00:00:00+00:00";

    let switch = |i: usize| {
        let json = match i % 5 {
            0 => settings_json(1, i.is_multiple_of(2), 0, 1, future),
            1 => settings_json(2, false, 0, 1 + (i % 50) as u32, future),
            2 => settings_json(2, true, 0, 1 + (i % 50) as u32, future),
            3 => settings_json(3, i.is_multiple_of(3), 0, 1, future),
            _ => settings_json(0, false, 0, 1, future),
        };
        sandbox.write_settings(&json);
        std::thread::sleep(Duration::from_millis(70));
    };

    for i in 0..50 {
        switch(i);
    }
    // A leak grows with every switch; noise does not. Run two equal phases and compare them.
    let settle = || std::thread::sleep(Duration::from_millis(1500));
    settle();
    let entered_before = sandbox.log().matches("Entered ").count();
    let phase = |label: &str| {
        for i in 0..200 {
            switch(i);
            if i % 50 == 0 {
                assert!(
                    responsive(window, 2 * SECOND),
                    "window thread unresponsive during {label} at {i}"
                );
            }
        }
        settle();
        resources(&awake)
    };
    let first = phase("phase 1");
    let second = phase("phase 2");
    let log = sandbox.log();
    let transitions = log.matches("Entered ").count() - entered_before;
    println!(
        "after phase 1: {first:?}
after phase 2: {second:?}
transitions applied: {transitions} of 400"
    );

    // How many writes Windows merges into one change notification depends on the machine's
    // load, so the count only has to show that reloads kept flowing; a stalled reload path
    // applies almost none. What must hold on any machine is that the last write wins.
    assert!(transitions >= 100, "reloads kept up: {transitions} of 400");
    let last = log.lines().rfind(|line| line.contains("Entered "));
    assert!(
        last.is_some_and(|line| line.ends_with("Entered Passive.")),
        "the last switch (Passive) was applied: {last:?}"
    );

    let growth = grown(&first, &second);
    if !growth.is_empty() {
        // A one-off step (a new heap segment, a log file rolling over) does not repeat; a leak
        // grows again by about as much in the next equal phase.
        let third = phase("phase 3");
        println!("after phase 3: {third:?}");
        let again = grown(&second, &third);
        assert!(
            again.is_empty(),
            "kept growing: {} then {}",
            growth.join(", "),
            again.join(", ")
        );
    }

    sandbox.signal_exit();
    assert_eq!(wait_exit(&mut awake, 5 * SECOND), Some(0));
}

/// What grew from one soak phase to the next by more than noise allows. The handle margin covers
/// Windows' own thread-pool and ETW handles; private bytes allow one heap segment.
fn grown(before: &Resources, after: &Resources) -> Vec<String> {
    let mut grown = Vec::new();
    if after.handles > before.handles + 2 {
        grown.push(format!("handles {} -> {}", before.handles, after.handles));
    }
    if after.gdi_objects > before.gdi_objects {
        grown.push(format!("GDI {} -> {}", before.gdi_objects, after.gdi_objects));
    }
    if after.user_objects > before.user_objects {
        grown.push(format!("USER {} -> {}", before.user_objects, after.user_objects));
    }
    // Awake owns three threads; Windows adds and retires thread-pool workers on its own.
    if after.threads > 8 {
        grown.push(format!("threads {}", after.threads));
    }
    if after.private_bytes > before.private_bytes + 64 * 1024 {
        grown.push(format!(
            "private bytes {} -> {}",
            before.private_bytes, after.private_bytes
        ));
    }
    grown
}

#[test]
fn legacy_options_hand_off_and_keep_the_parent_binding() {
    let _serial = serial();
    assert_running_in_a_console();
    let sandbox = Sandbox::new("handoff");
    // From a shell, the original options return at once and continue in a detached process.
    let started = Instant::now();
    let caller = output_within(
        &mut sandbox.command_in_callers_console(&["-t", "60", "-u"]),
        PROMPTLY,
    );
    assert_eq!(caller.status.code(), Some(0));
    assert!(started.elapsed() < 5 * SECOND);
    let expected = format!(
        "Continuing detached: [\"for\", \"60s\", \"--pid\", \"{}\", \"--handoff\"",
        std::process::id()
    );
    assert!(
        sandbox.log().contains(&expected),
        "the caller's parent became an explicit --pid:\n{}",
        sandbox.log()
    );
    assert!(
        sandbox.wait_for_log("Entered Timed.", 1, 5 * SECOND),
        "the detached session runs"
    );

    sandbox.signal_exit();
    assert!(sandbox.wait_for_log("Exit event signaled.", 1, 5 * SECOND));
}

#[test]
fn legacy_validation_errors_reach_the_caller() {
    let _serial = serial();
    assert_running_in_a_console();
    let sandbox = Sandbox::new("handoff-error");
    let caller = output_within(
        &mut sandbox.command_in_callers_console(&["-t", "60", "--pid", "999999"]),
        PROMPTLY,
    );
    assert_eq!(caller.status.code(), Some(1));
    assert!(String::from_utf8_lossy(&caller.stderr).contains("No running process"));
    assert!(!sandbox.log().contains("Continuing detached"));
}

#[test]
fn binding_to_the_waiting_caller_detaches_instead_of_deadlocking() {
    let _serial = serial();
    assert_running_in_a_console();
    let sandbox = Sandbox::new("caller-pid");
    // A script running `PowerToys.Awake --pid $PID` waits for Awake while Awake waits for it. A
    // regression would be that deadlock, so the wait is bounded and the caller killed.
    let started = Instant::now();
    let caller = output_within(
        &mut sandbox.command_in_callers_console(&["for", "1m", "--pid", &own_pid()]),
        PROMPTLY,
    );
    assert_eq!(caller.status.code(), Some(0));
    assert!(
        started.elapsed() < 10 * SECOND,
        "returned instead of waiting forever"
    );
    assert!(
        sandbox.wait_for_log("Entered Timed.", 1, 5 * SECOND),
        "the detached session runs"
    );
    assert!(
        String::from_utf8_lossy(&caller.stdout).is_empty(),
        "no live display in a pipe"
    );

    sandbox.signal_exit();
    assert!(sandbox.wait_for_log("Exit event signaled.", 1, 5 * SECOND));
}
