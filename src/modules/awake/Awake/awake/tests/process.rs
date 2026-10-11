//! End-to-end tests of the real executable, each in its own sandbox so they never touch a real
//! PowerToys installation. They run one at a time because some read machine-wide power state.

mod support;

use std::fs::{self, OpenOptions};
use std::os::windows::fs::OpenOptionsExt;
use std::os::windows::process::CommandExt;
use std::process::Command;
use std::sync::{Mutex, MutexGuard, PoisonError};
use std::time::{Duration, Instant};

use awake_core::mode::Mode;
use awake_core::settings::{ModeSetting, SettingsDocument};
use awake_core::strings::{StringId, Strings};
use awake_core::time::FixedZone;
use awake_testkit::log_lines::{self, entered, execution_state_set};
use awake_testkit::process::{Footprint, console_hosts, find_window, footprint, thread_names};
use awake_testkit::{hook_names, runner};
use support::*;
use windows_sys::Win32::Storage::FileSystem::{FILE_SHARE_READ, FILE_SHARE_WRITE};
use windows_sys::Win32::System::Console::GetConsoleProcessList;
use windows_sys::Win32::System::Threading::{CREATE_NEW_PROCESS_GROUP, CREATE_NO_WINDOW, DETACHED_PROCESS};

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

/// User-facing text, as the sandbox's English `language.json` makes Awake show it.
fn english(id: StringId) -> &'static str {
    Strings::ENGLISH.get(id)
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
        sandbox.wait_for_log(&execution_state_set(0x8000_0001), 1, 5 * SECOND),
        "{}",
        sandbox.log()
    );
    assert_ne!(
        system_execution_state() & 0x1,
        0,
        "Windows reports the system as required"
    );

    sandbox.write_settings(&settings_json(2, false, 0, 30, "2026-01-01T00:00:00+00:00"));
    assert!(sandbox.wait_for_log(&entered(Mode::Timed), 1, 5 * SECOND));

    // Turning the display on during a countdown must not restart it.
    sandbox.write_settings(&settings_json(2, true, 0, 30, "2026-01-01T00:00:00+00:00"));
    assert!(sandbox.wait_for_log(&execution_state_set(0x8000_0003), 1, 5 * SECOND));
    std::thread::sleep(Duration::from_millis(300));
    assert_eq!(
        sandbox.log().matches(&entered(Mode::Timed)).count(),
        1,
        "the countdown was not restarted"
    );

    // Saving the same session again, as Command Palette does, starts the countdown over.
    sandbox.write_settings(&settings_json(2, true, 0, 30, "2026-01-01T00:00:00+00:00"));
    assert!(
        sandbox.wait_for_log(&entered(Mode::Timed), 2, 5 * SECOND),
        "{}",
        sandbox.log()
    );
    std::thread::sleep(Duration::from_millis(300));
    assert_eq!(
        sandbox.log().matches(&entered(Mode::Timed)).count(),
        2,
        "restarted once per save, however many change notifications it caused"
    );

    // An expiration already in the past is moved five minutes ahead and saved back.
    sandbox.write_settings(&settings_json(3, true, 0, 30, "2020-01-01T00:00:00+00:00"));
    assert!(sandbox.wait_for_log(&entered(Mode::Expirable), 1, 5 * SECOND));
    assert!(sandbox.wait_for_log(log_lines::SAVED, 1, 5 * SECOND));
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
        sandbox.wait_for_log(&entered(Mode::Indefinite), 1, 5 * SECOND),
        "{}",
        sandbox.log()
    );

    target.kill().unwrap();
    target.wait().unwrap();
    assert_eq!(wait_exit(&mut awake, 5 * SECOND), Some(0));
    let output = awake.wait_with_output().unwrap();
    assert!(String::from_utf8_lossy(&output.stdout).contains(english(StringId::CliTargetExited)));
}

#[test]
fn a_second_instance_is_refused() {
    let _serial = serial();
    let sandbox = Sandbox::new("single");
    let mut first = sandbox.spawn(&["for", "1m"]);
    assert!(sandbox.wait_for_log(&entered(Mode::Timed), 1, 5 * SECOND));

    let started = Instant::now();
    let second = output_within(&mut sandbox.command(&["for", "1m"]), PROMPTLY);
    assert_eq!(second.status.code(), Some(1));
    assert!(String::from_utf8_lossy(&second.stderr).contains(english(StringId::CliErrAlreadyRunning)));
    assert!(started.elapsed() < 3 * SECOND, "refused promptly");
    assert_eq!(
        first.try_wait().unwrap(),
        None,
        "the first instance keeps running"
    );

    sandbox.signal_exit();
    assert_eq!(wait_exit(&mut first, 5 * SECOND), Some(0));
}

/// A program that took Awake's single-instance mutex first, as any process in the session can, is
/// refused, and the user is told why rather than shown "Access is denied".
#[test]
fn a_mutex_another_program_planted_is_refused_with_an_explanation() {
    let _serial = serial();
    let sandbox = Sandbox::new("planted");
    let _planted = sandbox.planted_instance_mutex();
    let output = output_within(&mut sandbox.command(&["for", "1m"]), PROMPTLY);
    assert_eq!(output.status.code(), Some(1));
    let stderr = String::from_utf8_lossy(&output.stderr);
    assert!(stderr.contains(english(StringId::CliErrNameTaken)), "{stderr}");
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
            .env(hook_names::HANDOVER_MS, "500"),
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
                .env(hook_names::HANG_ON_EXIT, "1")
                .env(hook_names::WATCHDOG_MS, "1000")
                .spawn()
                .unwrap(),
        )
    };

    // Stopped by the runner's exit event.
    let sandbox = Sandbox::new("watchdog-exit");
    let mut awake = hung(&sandbox, &["for", "1m", "--pid", &own_pid()]);
    assert!(sandbox.wait_for_log(&entered(Mode::Timed), 1, 5 * SECOND));
    sandbox.signal_exit();
    assert_eq!(wait_exit(&mut awake, 5 * SECOND), Some(1), "{}", sandbox.log());
    assert!(
        sandbox.log().contains(log_lines::ENDING_HUNG_PROCESS),
        "{}",
        sandbox.log()
    );

    // A command-line session whose time is up.
    let sandbox = Sandbox::new("watchdog-deadline");
    let mut awake = hung(&sandbox, &["for", "2s", "--pid", &own_pid()]);
    assert_eq!(wait_exit(&mut awake, 8 * SECOND), Some(1), "{}", sandbox.log());
    assert!(
        sandbox.log().contains(log_lines::ENDING_HUNG_PROCESS),
        "{}",
        sandbox.log()
    );
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
    assert!(String::from_utf8_lossy(&output.stdout).contains(english(StringId::CliFinished)));
}

#[test]
fn invalid_requests_fail_fast_with_a_message() {
    let _serial = serial();
    let sandbox = Sandbox::new("invalid");
    let strings = Strings::ENGLISH;
    for (args, needle) in [
        (
            vec!["until", "2000-01-01T00:00:00Z"],
            english(StringId::CliErrPast).to_owned(),
        ),
        (
            vec!["--pid", "999999"],
            strings.format(StringId::CliErrPidNotFound, "999999"),
        ),
        (
            vec!["for", "soon"],
            strings.format(StringId::CliErrDuration, "soon"),
        ),
        (vec!["-t", "abc"], strings.format(StringId::CliErrSeconds, "abc")),
        // clap's own wording, which Awake passes on.
        (vec!["--bogus"], "unexpected argument".to_owned()),
    ] {
        let output = output_within(&mut sandbox.command(&args), PROMPTLY);
        assert_eq!(output.status.code(), Some(1), "{args:?}");
        assert!(
            String::from_utf8_lossy(&output.stderr).contains(&needle),
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
    assert!(sandbox.wait_for_log(&entered(Mode::Indefinite), 1, 5 * SECOND));

    sandbox.write_settings("{\"name\":\"Awake\",\"properties\":{\"mode\":");
    assert!(
        sandbox.wait_for_log(log_lines::SETTINGS_UNREADABLE, 1, 5 * SECOND),
        "{}",
        sandbox.log()
    );
    assert_eq!(
        fs::read_to_string(sandbox.settings_path()).unwrap(),
        "{\"name\":\"Awake\",\"properties\":{\"mode\":"
    );
    assert_eq!(
        sandbox.log().matches(&entered(Mode::Passive)).count(),
        0,
        "the session was not dropped"
    );

    sandbox.write_settings(&settings_json(0, false, 0, 1, "2026-01-01T00:00:00+00:00"));
    assert!(sandbox.wait_for_log(&entered(Mode::Passive), 1, 5 * SECOND));
    sandbox.signal_exit();
    assert_eq!(wait_exit(&mut awake, 5 * SECOND), Some(0));
}

#[test]
fn an_unknown_mode_is_reported_at_startup_too() {
    let _serial = serial();
    let sandbox = Sandbox::new("unknown-mode");
    sandbox.write_settings(&settings_json(9, false, 0, 1, "2026-01-01T00:00:00+00:00"));
    let mut awake = sandbox.spawn(&["--use-pt-config", "--pid", &own_pid()]);
    assert!(
        sandbox.wait_for_log(&log_lines::unknown_mode(9), 1, 5 * SECOND),
        "{}",
        sandbox.log()
    );
    sandbox.signal_exit();
    assert_eq!(wait_exit(&mut awake, 5 * SECOND), Some(0));
}

#[test]
fn a_removed_settings_file_keeps_the_mode_until_it_is_written_again() {
    let _serial = serial();
    let sandbox = Sandbox::new("removed");
    sandbox.write_settings(&settings_json(1, false, 0, 1, "2026-01-01T00:00:00+00:00"));
    let mut awake = sandbox.spawn(&["--use-pt-config", "--pid", &own_pid()]);
    assert!(sandbox.wait_for_log(&entered(Mode::Indefinite), 1, 5 * SECOND));

    fs::remove_file(sandbox.settings_path()).unwrap();
    assert!(
        sandbox.wait_for_log(log_lines::SETTINGS_REMOVED, 1, 5 * SECOND),
        "{}",
        sandbox.log()
    );
    // Another change notification while the file is gone says nothing new.
    sandbox.write_settings("");
    fs::remove_file(sandbox.settings_path()).unwrap();
    std::thread::sleep(Duration::from_millis(500));
    let log = sandbox.log();
    assert_eq!(log.matches(log_lines::SETTINGS_REMOVED).count(), 1, "{log}");
    assert!(!log.contains(log_lines::SETTINGS_UNREADABLE), "{log}");
    assert!(!log.contains(&entered(Mode::Passive)), "{log}");

    sandbox.write_settings(&settings_json(0, false, 0, 1, "2026-01-01T00:00:00+00:00"));
    assert!(sandbox.wait_for_log(&entered(Mode::Passive), 1, 5 * SECOND));
    sandbox.signal_exit();
    assert_eq!(wait_exit(&mut awake, 5 * SECOND), Some(0));
}

/// A session that ends while another program holds the settings file waits to save that. Exiting
/// meanwhile must not drop the save: the next start would read a session that already ended.
#[test]
fn a_save_still_waiting_on_a_busy_file_is_tried_again_on_exit() {
    let _serial = serial();
    // Readable, but not replaceable, as while another program has it open.
    let hold = |sandbox: &Sandbox| {
        OpenOptions::new()
            .read(true)
            .share_mode(FILE_SHARE_READ | FILE_SHARE_WRITE)
            .open(sandbox.settings_path())
            .unwrap()
    };
    let start = |name: &str| {
        let sandbox = Sandbox::new(name);
        sandbox.write_settings(&settings_json(3, false, 0, 1, &utc_in(3)));
        let awake = sandbox.spawn(&["--use-pt-config", "--pid", &own_pid()]);
        assert!(sandbox.wait_for_log(&entered(Mode::Expirable), 1, 5 * SECOND));
        (sandbox, awake)
    };

    // The file comes free just as Awake is asked to exit. The regular retry may win the race
    // and save first, so this part only shows the save lands; the next part pins the exit path.
    let (sandbox, mut awake) = start("busy-exit");
    let busy = hold(&sandbox);
    assert!(sandbox.wait_for_log(&entered(Mode::Passive), 1, 10 * SECOND));
    std::thread::sleep(Duration::from_millis(300));
    assert!(!sandbox.log().contains(log_lines::SAVED), "{}", sandbox.log());
    drop(busy);
    sandbox.signal_exit();
    assert_eq!(wait_exit(&mut awake, 5 * SECOND), Some(0));
    assert_eq!(
        read_settings(&sandbox).mode,
        ModeSetting::Known(Mode::Passive),
        "{}",
        sandbox.log()
    );

    // Still busy at exit: the save is given up on visibly, not dropped in silence.
    let (sandbox, mut awake) = start("busy-gone");
    let busy = hold(&sandbox);
    assert!(sandbox.wait_for_log(&entered(Mode::Passive), 1, 10 * SECOND));
    sandbox.signal_exit();
    assert_eq!(wait_exit(&mut awake, 5 * SECOND), Some(0));
    let log = sandbox.log();
    let exit = log.find(log_lines::EXIT_EVENT_SIGNALED).expect("exit logged");
    assert!(log[exit..].contains(log_lines::SAVE_FAILED), "{log}");
    drop(busy);
}

/// The runner sets the exit event to stop the Awake it started and keeps it for a few seconds.
/// A command-line session that starts in that window must not take the signal for itself.
#[test]
fn a_command_line_start_ignores_a_stop_meant_for_the_previous_instance() {
    let _serial = serial();
    let sandbox = Sandbox::new("stale-exit");
    let stale = sandbox.signaled_exit_event();
    let mut awake = sandbox.spawn(&["for", "1m"]);
    assert!(sandbox.wait_for_log(&entered(Mode::Timed), 1, 5 * SECOND));
    std::thread::sleep(Duration::from_millis(500));
    assert_eq!(awake.try_wait().unwrap(), None, "{}", sandbox.log());
    sandbox.signal_exit();
    assert_eq!(wait_exit(&mut awake, 5 * SECOND), Some(0));
    drop(stale);

    // A PowerToys start keeps the signal: the runner may stop it before it gets that far.
    let sandbox = Sandbox::new("early-exit");
    let early = sandbox.signaled_exit_event();
    let mut awake = sandbox.spawn(&["--use-pt-config", "--pid", &own_pid()]);
    assert_eq!(wait_exit(&mut awake, 5 * SECOND), Some(0), "{}", sandbox.log());
    assert!(sandbox.log().contains(log_lines::EXIT_EVENT_SIGNALED));
    drop(early);
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
    let probe = runner::launch(&cmd, &["/c", "ping -n 5 127.0.0.1"], &[], CREATE_NO_WINDOW).unwrap();
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

/// Ctrl+C and Ctrl+Break end a session cleanly. The handler keeps Windows from ending the
/// process, so its own watchdog is what ends one whose window thread hangs.
#[test]
fn ctrl_break_ends_the_session_and_the_watchdog_backs_it_up() {
    let _serial = serial();
    assert_running_in_a_console();
    let start = |sandbox: &Sandbox, hang: bool| {
        let mut command = sandbox.command_in_callers_console(&["for", "1m"]);
        command.creation_flags(CREATE_NEW_PROCESS_GROUP);
        if hang {
            command
                .env(hook_names::HANG_ON_EXIT, "1")
                .env(hook_names::WATCHDOG_MS, "1000");
        }
        let awake = Running::new(command.spawn().unwrap());
        assert!(
            sandbox.wait_for_log(&entered(Mode::Timed), 1, 5 * SECOND),
            "{}",
            sandbox.log()
        );
        awake
    };

    let sandbox = Sandbox::new("ctrl-break");
    let mut awake = start(&sandbox, false);
    ctrl_break(awake.id());
    assert_eq!(wait_exit(&mut awake, 5 * SECOND), Some(0), "{}", sandbox.log());
    let log = sandbox.log();
    assert!(log.contains(&log_lines::console_control_event(1)), "{log}");
    assert!(log.contains(&log_lines::exiting(0)), "{log}");

    let sandbox = Sandbox::new("ctrl-break-hung");
    let mut awake = start(&sandbox, true);
    ctrl_break(awake.id());
    assert_eq!(wait_exit(&mut awake, 5 * SECOND), Some(1), "{}", sandbox.log());
    assert!(
        sandbox.log().contains(log_lines::ENDING_HUNG_PROCESS),
        "{}",
        sandbox.log()
    );
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
    assert!(sandbox.log().contains(log_lines::CLOSING_FOR_UPDATE));
    assert!(
        sandbox.log().contains(&log_lines::exiting(0)),
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
    assert!(sandbox.wait_for_log(&entered(Mode::Expirable), 1, 5 * SECOND));
    assert!(
        sandbox.wait_for_log(log_lines::SAVE_FAILED, 1, 10 * SECOND),
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
    let entered_before = sandbox.log().matches(log_lines::ENTERED).count();
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
        footprint(&*awake, awake.id())
    };
    let first = phase("phase 1");
    let second = phase("phase 2");
    let log = sandbox.log();
    let transitions = log.matches(log_lines::ENTERED).count() - entered_before;
    println!(
        "after phase 1: {first:?}
after phase 2: {second:?}
transitions applied: {transitions} of 400"
    );

    // How many writes Windows merges into one change notification depends on the machine's
    // load, so the count only has to show that reloads kept flowing; a stalled reload path
    // applies almost none. What must hold on any machine is that the last write wins.
    assert!(transitions >= 100, "reloads kept up: {transitions} of 400");
    let last = log.lines().rfind(|line| line.contains(log_lines::ENTERED));
    assert!(
        last.is_some_and(|line| line.ends_with(&entered(Mode::Passive))),
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
fn grown(before: &Footprint, after: &Footprint) -> Vec<String> {
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
    // How long the detached process takes to confirm depends on the machine's load, so the log,
    // not the clock, shows that the caller returned on its confirmation.
    let caller = output_within(
        &mut sandbox.command_in_callers_console(&["-t", "60", "-u"]),
        PROMPTLY,
    );
    assert_eq!(caller.status.code(), Some(0));
    assert!(
        sandbox.log().contains(log_lines::HANDOFF_CONFIRMED),
        "{}",
        sandbox.log()
    );
    let pid = own_pid();
    let resolved = format!("{:?}", ["for", "60s", "--pid", pid.as_str(), "--handoff"]);
    let expected = format!(
        "{} {}",
        log_lines::CONTINUING_DETACHED,
        resolved.trim_end_matches(']')
    );
    assert!(
        sandbox.log().contains(&expected),
        "the caller's parent became an explicit --pid:\n{}",
        sandbox.log()
    );
    assert!(
        sandbox.wait_for_log(&entered(Mode::Timed), 1, 5 * SECOND),
        "the detached session runs"
    );

    sandbox.signal_exit();
    assert!(sandbox.wait_for_log(log_lines::EXIT_EVENT_SIGNALED, 1, 5 * SECOND));
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
    assert!(
        String::from_utf8_lossy(&caller.stderr)
            .contains(&Strings::ENGLISH.format(StringId::CliErrPidNotFound, "999999"))
    );
    assert!(!sandbox.log().contains(log_lines::CONTINUING_DETACHED));
}

#[test]
fn binding_to_the_waiting_caller_detaches_instead_of_deadlocking() {
    let _serial = serial();
    assert_running_in_a_console();
    let sandbox = Sandbox::new("caller-pid");
    // A script running `PowerToys.Awake --pid $PID` waits for Awake while Awake waits for it. A
    // regression would be that deadlock, so the wait is bounded and the caller killed.
    let caller = output_within(
        &mut sandbox.command_in_callers_console(&["for", "1m", "--pid", &own_pid()]),
        PROMPTLY,
    );
    assert_eq!(caller.status.code(), Some(0));
    assert!(
        sandbox.log().contains(log_lines::HANDOFF_CONFIRMED),
        "returned once the detached session confirmed:\n{}",
        sandbox.log()
    );
    assert!(
        sandbox.wait_for_log(&entered(Mode::Timed), 1, 5 * SECOND),
        "the detached session runs"
    );
    assert!(
        String::from_utf8_lossy(&caller.stdout).is_empty(),
        "no live display in a pipe"
    );

    sandbox.signal_exit();
    assert!(sandbox.wait_for_log(log_lines::EXIT_EVENT_SIGNALED, 1, 5 * SECOND));
}
