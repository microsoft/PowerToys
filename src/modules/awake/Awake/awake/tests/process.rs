//! End-to-end tests of the real executable, each in its own sandbox so they never touch a real
//! PowerToys installation. They run one at a time because some read machine-wide power state.

mod support;

use std::fs;
use std::process::Command;
use std::sync::{Mutex, MutexGuard};
use std::time::{Duration, Instant};

use awake_core::mode::Mode;
use awake_core::settings::{ModeSetting, SettingsDocument};
use awake_core::time::FixedZone;
use support::*;

const SECOND: Duration = Duration::from_secs(1);

fn serial() -> MutexGuard<'static, ()> {
    static LOCK: Mutex<()> = Mutex::new(());
    LOCK.lock().unwrap_or_else(|poisoned| poisoned.into_inner())
}

fn own_pid() -> String {
    std::process::id().to_string()
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
    let mut target = Command::new("cmd.exe")
        .args(["/c", "ping -n 60 127.0.0.1 >nul"])
        .spawn()
        .unwrap();
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
    let second = sandbox.command(&["for", "1m"]).output().unwrap();
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
fn a_command_line_session_ends_on_time() {
    let _serial = serial();
    let sandbox = Sandbox::new("timed");
    let started = Instant::now();
    let output = sandbox.command(&["for", "2s"]).output().unwrap();
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
        let output = sandbox.command(&args).output().unwrap();
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

#[test]
fn mode_switches_do_not_leak_or_hang() {
    let _serial = serial();
    let sandbox = Sandbox::new("soak");
    let mut awake = sandbox.spawn(&["-c", "--pid", &own_pid()]);
    let window = find_window(awake.id(), 5 * SECOND).expect("tray window");
    let future = "2099-01-01T00:00:00+00:00";

    let switch = |i: usize| {
        let json = match i % 5 {
            0 => settings_json(1, i % 2 == 0, 0, 1, future),
            1 => settings_json(2, false, 0, 1 + (i % 50) as u32, future),
            2 => settings_json(2, true, 0, 1 + (i % 50) as u32, future),
            3 => settings_json(3, i % 3 == 0, 0, 1, future),
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
    let transitions = sandbox.log().matches("Entered ").count() - entered_before;

    println!(
        "after phase 1: {first:?}
after phase 2: {second:?}
transitions applied: {transitions} of 400"
    );
    assert!(transitions >= 300, "most switches were applied: {transitions}");
    assert!(
        second.handles <= first.handles + 2,
        "handles {} -> {}",
        first.handles,
        second.handles
    );
    assert!(
        second.gdi_objects <= first.gdi_objects,
        "GDI {} -> {}",
        first.gdi_objects,
        second.gdi_objects
    );
    assert!(
        second.user_objects <= first.user_objects,
        "USER {} -> {}",
        first.user_objects,
        second.user_objects
    );
    // Awake owns three threads; Windows adds and retires thread-pool workers on its own.
    assert!(second.threads <= 8, "threads {}", second.threads);
    assert!(
        second.private_bytes <= first.private_bytes + 64 * 1024,
        "private bytes {} -> {}",
        first.private_bytes,
        second.private_bytes
    );

    sandbox.signal_exit();
    assert_eq!(wait_exit(&mut awake, 5 * SECOND), Some(0));
}

#[test]
fn legacy_options_hand_off_and_keep_the_parent_binding() {
    let _serial = serial();
    let sandbox = Sandbox::new("handoff");
    // From a shell, the original options return at once and continue in a detached process.
    let started = Instant::now();
    let caller = sandbox
        .command_in_callers_console(&["-t", "60", "-u"])
        .output()
        .unwrap();
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
    let sandbox = Sandbox::new("handoff-error");
    let caller = sandbox
        .command_in_callers_console(&["-t", "60", "--pid", "999999"])
        .output()
        .unwrap();
    assert_eq!(caller.status.code(), Some(1));
    assert!(String::from_utf8_lossy(&caller.stderr).contains("No running process"));
    assert!(!sandbox.log().contains("Continuing detached"));
}

#[test]
fn binding_to_the_waiting_caller_detaches_instead_of_deadlocking() {
    let _serial = serial();
    let sandbox = Sandbox::new("caller-pid");
    // A script running `PowerToys.Awake --pid $PID` waits for Awake while Awake waits for it.
    let started = Instant::now();
    let caller = sandbox
        .command_in_callers_console(&["for", "1m", "--pid", &own_pid()])
        .output()
        .unwrap();
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
