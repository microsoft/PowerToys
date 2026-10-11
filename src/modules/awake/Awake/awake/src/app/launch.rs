//! From the command line to a running session: parsing, the checks a start must pass, and the
//! hand-off to a detached process when the caller's shell would otherwise wait on Awake.

use std::cell::RefCell;
use std::ffi::OsString;
use std::fmt::Display;
use std::os::windows::io::AsRawHandle;
use std::time::Duration;
use std::{fs, io, ptr};

use awake_core::cli::{self, Binding, Invocation, Plan, Request, UntilSpec};
use awake_core::engine::{Activity, Engine, Source};
use awake_core::powertoys;
use awake_core::settings::FILE_NAME;
use awake_core::strings::{StringId, Strings};
use awake_core::text::{self, Style};
use awake_core::time::DateTimeOffset;
use awake_core::tray_model;
use windows_sys::Win32::System::Threading::ResetEvent;
use windows_sys::Win32::UI::WindowsAndMessaging::{DestroyWindow, WM_SETTINGCHANGE};

use super::window_proc::{self, window_proc};
use super::{App, Outcome, SESSION_RETRY_MS, SettingsFile, Start, session_summary};
use crate::bridge::{self, Policy, Telemetry};
use crate::platform::clock::SystemClock;
use crate::platform::console::{self, LiveOutput, Output};
use crate::platform::instance::{self, InstanceLock};
use crate::platform::messages::{TIMER_SESSION, WM_TRAY};
use crate::platform::paths::Paths;
use crate::platform::process::{self, BindError, BoundProcess, Started};
use crate::platform::settings_store::{self, SettingsStore};
use crate::platform::tray::Tray;
use crate::platform::waiter::{Waiter, WaiterSetup};
use crate::platform::{console_ctrl, event, last_error, locale, session, theme, window};
use crate::{BUILD_ID, VERSION, log_lines, logging};

/// How long a PowerToys-managed start waits for a previous instance that is still shutting down.
const INSTANCE_HANDOVER: Duration = Duration::from_secs(5);
/// How long the caller of a hand-off waits for the detached process to confirm its session.
/// Past it, the session most likely runs and only the confirmation is late.
const HANDOFF_WAIT: Duration = Duration::from_secs(15);

/// Parses the command line and runs what it asks for; returns the process exit code.
pub(super) fn start() -> i32 {
    let paths = Paths::resolve();
    let language = locale::ui_language(paths.as_ref().ok());
    let strings = Strings::for_language(&language);
    let output = console::detect();
    let style = match output {
        Output::Console(style) => style,
        Output::Redirected | Output::None => Style::PLAIN,
    };

    let request = match cli::parse(std::env::args_os()) {
        Ok(request) => request,
        Err(error) => {
            Telemetry::start().cli_command(false);
            console::write_stderr(&text::error(
                strings,
                style,
                &text::cli_error_message(strings, &error),
            ));
            return 1;
        }
    };
    let invocation = match request {
        Request::Help => {
            console::write_stdout(&text::help(strings, style, VERSION));
            return 0;
        }
        Request::Version => {
            console::write_stdout(&text::version_line(VERSION, BUILD_ID));
            return 0;
        }
        Request::Run(invocation) => invocation,
    };

    let paths = match paths {
        Ok(paths) => paths,
        Err(error) => {
            console::write_stderr(&text::error(
                strings,
                style,
                &start_error_message(strings, &error),
            ));
            return 1;
        }
    };
    let log_level = settings_store::read_bounded(&paths.log_settings_file())
        .ok()
        .and_then(|bytes| powertoys::log_level(&bytes));
    logging::init(&paths.log_dir(), VERSION, log_level);
    log::info!("PowerToys Awake {VERSION} ({BUILD_ID}) starting: {invocation:?}, language {language:?}");

    // Looked up once: it takes a snapshot of every process on the machine.
    let parent = match invocation.binding {
        Binding::Parent => process::parent_pid(),
        Binding::Pid(_) | Binding::None => None,
    };
    let mut launcher = Launcher {
        paths,
        strings,
        language,
        style,
        output,
        parent,
        telemetry: Telemetry::start(),
    };
    let attached = console::attached_processes();
    let shell_waits = attached.len() > 1;
    // A console made just for Awake by a program without one: drop it for sessions that are
    // bound to a process or run under PowerToys, which never showed a window. Its host process
    // would otherwise live as long as Awake, and a live status line would be drawn into it
    // every second. Only Awake being attached is the test: a console created with
    // CREATE_NO_WINDOW has no window, so GetConsoleWindow cannot see it.
    if attached.len() == 1 && (invocation.binding != Binding::None || invocation.plan == Plan::PowerToys) {
        console::detach();
        // Output redirected to a pipe or a file still reaches whoever reads it.
        if matches!(launcher.output, Output::Console(_)) {
            launcher.output = Output::None;
        }
    }

    // Waiting in the foreground for a process that is itself waiting for Awake (a script
    // running `--pid $PID`, or `--parent` from a shell) would never end, so detach instead.
    let target = match invocation.binding {
        Binding::Pid(pid) => u32::try_from(pid).ok(),
        Binding::Parent => launcher.parent,
        Binding::None => None,
    };
    let caller_waits_for_target = target.is_some_and(|pid| attached.contains(&pid));
    let result = if shell_waits && (invocation.legacy || caller_waits_for_target) {
        launcher.hand_off(&invocation)
    } else {
        launcher.launch(&invocation)
    };
    result.unwrap_or(1)
}

/// A start Awake turned down. The reason has already been shown and logged; the exit code is 1.
struct Refused;

/// Everything checked before Awake commits to running: a refused start never shows an icon.
struct Prepared {
    instance: InstanceLock,
    start: Start,
    bound: Option<BoundProcess>,
}

struct Launcher {
    paths: Paths,
    strings: Strings,
    language: String,
    style: Style,
    output: Output,
    /// The parent process for `--parent`, when there is one.
    parent: Option<u32>,
    telemetry: Telemetry,
}

impl Launcher {
    fn prepare(&self, invocation: &Invocation) -> Result<Prepared, Refused> {
        let handover = if invocation.plan == Plan::PowerToys || invocation.handoff_event.is_some() {
            crate::hooks::handover(INSTANCE_HANDOVER)
        } else {
            Duration::ZERO
        };
        let instance = match instance::acquire(&self.paths.instance_mutex_name(), handover) {
            Ok(Some(lock)) => lock,
            Ok(None) => return Err(self.refuse(self.strings.get(StringId::CliErrAlreadyRunning))),
            Err(error) => return Err(self.refuse_named_object(&error)),
        };
        if bridge::policy() == Policy::Disabled {
            return Err(self.refuse(self.strings.get(StringId::CliErrPolicy)));
        }

        let clock = SystemClock;
        let now = clock.now();
        let start = match &invocation.plan {
            Plan::PowerToys => Start::PowerToys,
            Plan::Indefinite => Start::CommandLine(Activity::Indefinite),
            Plan::For { seconds } => Start::CommandLine(Activity::Timed {
                seconds: *seconds,
                deadline: now.utc.saturating_add_secs(*seconds),
            }),
            Plan::Until(spec) => match resolve_until(spec, now, self.strings) {
                Ok(until) => Start::CommandLine(Activity::Expirable { until }),
                Err(message) => return Err(self.refuse(&message)),
            },
        };

        let bound = match invocation.binding {
            Binding::None => None,
            Binding::Pid(pid) => match process::bind_pid(pid) {
                Ok(bound) => Some(bound),
                Err(error) => return Err(self.refuse(&bind_error_message(self.strings, error, Some(pid)))),
            },
            Binding::Parent => match process::bind_parent(self.parent) {
                Ok(bound) => Some(bound),
                Err(error) => return Err(self.refuse(&bind_error_message(self.strings, error, None))),
            },
        };
        Ok(Prepared {
            instance,
            start,
            bound,
        })
    }

    /// Validates in the caller's console, so mistakes are shown and the exit code reaches the
    /// shell, then continues in a detached process with everything already resolved: the parent
    /// for `-u` becomes an explicit `--pid`, and times become absolute.
    fn hand_off(&self, invocation: &Invocation) -> Result<i32, Refused> {
        let Prepared {
            instance,
            start,
            bound,
        } = self.prepare(invocation)?;
        let summary = (!invocation.legacy && matches!(self.output, Output::Console(_))).then(|| {
            let activity = match start {
                Start::CommandLine(activity) => activity,
                Start::PowerToys => Activity::Indefinite,
            };
            let target = bound.as_ref().map(|bound| bound.target.clone());
            session_summary(self.strings, SystemClock, activity, invocation.display_on, target)
        });
        let mut args: Vec<String> = Vec::new();
        match start {
            Start::PowerToys => args.push("--use-pt-config".into()),
            Start::CommandLine(Activity::Timed { seconds, .. }) => {
                args.extend(["for".into(), format!("{seconds}s")]);
            }
            Start::CommandLine(Activity::Expirable { until }) => {
                args.extend(["until".into(), until.to_json_string()]);
            }
            Start::CommandLine(Activity::Indefinite | Activity::Passive) => {}
        }
        if invocation.display_on {
            args.push("--display-on".into());
        }
        if let Some(bound) = &bound {
            args.extend(["--pid".into(), bound.target.pid.to_string()]);
        }
        let new_console = bound.is_none() && start.source() == Source::CommandLine;

        // The new process confirms through this event once its session runs, so the caller's
        // exit code still tells the truth.
        let ready_name = format!(
            "Local\\PowerToysAwakeHandoff-{}-{}{}",
            std::process::id(),
            SystemClock.now_utc().ticks(),
            self.paths.object_suffix
        );
        let ready =
            event::manual_reset(Some(&ready_name)).map_err(|error| self.refuse_named_object(&error))?;
        args.extend(["--handoff".into(), ready_name]);
        // Let the new process take the single-instance lock.
        drop(instance);
        let child = process::spawn_detached(&args, new_console).map_err(|error| self.refuse_start(&error))?;
        log::info!("{} {args:?}", log_lines::CONTINUING_DETACHED);
        let started = process::wait_for_start(&ready, &child, HANDOFF_WAIT);
        // Held until the new process has opened the target itself, so no other process can take
        // over the PID it was given in between.
        drop(bound);
        match started {
            Started::Confirmed => log::info!("{}", log_lines::HANDOFF_CONFIRMED),
            Started::NoWord => {
                log::warn!("No word from the detached session within {HANDOFF_WAIT:?}; it most likely runs.");
            }
            Started::Exited(code) => {
                log::warn!("The detached session ended right away with exit code {code}.");
                console::write_stderr(&text::error(
                    self.strings,
                    self.style,
                    &self
                        .strings
                        .format(StringId::CliErrHandoffExited, &code.to_string()),
                ));
                return Ok(code);
            }
        }
        if let Some(summary) = summary {
            console::write_stdout(&text::session_header(self.strings, self.style, VERSION, &summary));
        }
        Ok(0)
    }

    fn launch(self, invocation: &Invocation) -> Result<i32, Refused> {
        let Prepared {
            instance,
            start,
            bound,
        } = self.prepare(invocation)?;
        let source = start.source();

        let exit_name = self.paths.exit_event_name(&bridge::exit_event_name());
        // Opens the runner's event when it created it first.
        let exit_event =
            event::manual_reset(Some(&exit_name)).map_err(|error| self.refuse_named_object(&error))?;
        if source == Source::CommandLine {
            // A signal already set was meant for an Awake that has since exited: the runner sets
            // the event to stop the instance it started and keeps it open for a few seconds, and
            // this one could only take the lock once that instance let go. A PowerToys start keeps
            // the signal, because the runner may set it before Awake gets this far.
            // SAFETY: valid event handle.
            unsafe { ResetEvent(exit_event.as_raw_handle()) };
        }

        let window = window::create(Some(window_proc)).map_err(|error| self.refuse_start(&error))?;
        window_proc::register_taskbar_created(window);
        window::allow_message(window, WM_TRAY);
        // Theme changes are broadcast by Explorer and the Settings app, which run below an
        // elevated Awake's integrity level; without this, its menu keeps the start-up theme.
        window::allow_message(window, WM_SETTINGCHANGE);
        theme::apply_system_theme();
        let session_registered = match session::register(window) {
            Ok(()) => true,
            Err(error) => {
                log::warn!("Lock notifications are not available yet ({error}); will retry.");
                if !window::set_timer(window, TIMER_SESSION, SESSION_RETRY_MS) {
                    log::error!("Could not schedule the retry: {}", last_error());
                }
                false
            }
        };
        // Harmless without a console; with one, even a redirected one, Ctrl+C must clean up.
        console_ctrl::install(window);

        let store = (source == Source::PowerToys).then(|| SettingsStore::new(self.paths.settings_file()));
        let watch = store
            .as_ref()
            .map(|_| (self.paths.module_dir(), OsString::from(FILE_NAME)));
        if let Some(store) = &store {
            let _ = fs::create_dir_all(self.paths.module_dir());
            log::info!("Following {}", store.path().display());
        }
        let (bound, process_handle) = match bound {
            Some(BoundProcess { handle, target }) => (Some(target), Some(handle)),
            None => (None, None),
        };
        let waiter = match Waiter::start(WaiterSetup {
            window,
            exit_event,
            process: process_handle,
            watch,
            deadline_ends_process: source == Source::CommandLine,
        }) {
            Ok(waiter) => waiter,
            Err(error) => {
                // SAFETY: our own window, not yet in use.
                unsafe { DestroyWindow(window) };
                return Err(self.refuse_start(&error));
            }
        };

        let live = match (source, self.output) {
            (Source::CommandLine, Output::Console(_)) => LiveOutput::start().ok(),
            _ => None,
        };
        let presets = tray_model::default_presets(self.strings);
        let app = RefCell::new(App {
            window,
            strings: self.strings,
            right_to_left: locale::is_right_to_left(&self.language),
            clock: SystemClock,
            engine: Engine::new(source, presets, session::is_locked()),
            tray: Tray::new(window),
            telemetry: self.telemetry,
            store,
            pending_changes: Vec::new(),
            write_retries: 0,
            reload_retries: 0,
            settings_file: SettingsFile::Unseen,
            waiter: Some(waiter),
            bound,
            live,
            redirected: matches!(self.output, Output::Redirected),
            style: self.style,
            session_registered,
            session_retries: 0,
            exit: None,
            _instance: instance,
        });

        // SAFETY: `app` lives until after the message loop; the pointer is cleared below before
        // `app` is dropped, and the window procedure only runs on this thread.
        unsafe { window::set_user_data(window, ptr::from_ref(&app) as isize) };
        let outcome = app.borrow_mut().start(start, invocation.display_on);
        app.borrow().telemetry.cli_command(true);
        if outcome == Outcome::Destroy {
            // SAFETY: our own window.
            unsafe { DestroyWindow(window) };
        } else if let Some(name) = &invocation.handoff_event {
            // Only now is there a session to report; a start that failed on the way exits with
            // code 1, which the waiting caller relays.
            event::signal_named(name);
        }
        window::run_message_loop();
        // SAFETY: clearing the pointer set above; the window is already destroyed.
        unsafe { window::set_user_data(window, 0) };
        Ok(app.into_inner().finish())
    }

    fn refuse(&self, message: &str) -> Refused {
        log::warn!("Not starting: {message}");
        self.telemetry.cli_command(false);
        console::write_stderr(&text::error(self.strings, self.style, message));
        Refused
    }

    /// A start that failed for a technical reason, which the message passes on.
    fn refuse_start(&self, error: &dyn Display) -> Refused {
        self.refuse(&start_error_message(self.strings, error))
    }

    fn refuse_named_object(&self, error: &io::Error) -> Refused {
        log::warn!("Could not create or open a named kernel object: {error}");
        self.refuse(&named_object_message(self.strings, error))
    }
}

/// Why the single-instance mutex or an event could not be created or opened. Awake creates both
/// for the current user, so access denied means another program holds the name, which the
/// operating system's wording would not tell the user.
fn named_object_message(strings: Strings, error: &io::Error) -> String {
    if error.kind() == io::ErrorKind::PermissionDenied {
        strings.get(StringId::CliErrNameTaken).to_owned()
    } else {
        start_error_message(strings, error)
    }
}

/// A start that failed for a technical reason, which the message passes on.
fn start_error_message(strings: Strings, error: &dyn Display) -> String {
    strings.format(StringId::CliErrStart, &error.to_string())
}

/// When an `until` session ends, or the localized reason it cannot start.
fn resolve_until(spec: &UntilSpec, now: DateTimeOffset, strings: Strings) -> Result<DateTimeOffset, String> {
    let clock = SystemClock;
    cli::resolve_until(spec, &clock, &|text| clock.parse_regional(text), now)
        .map_err(|error| text::until_error_message(strings, &error))
}

/// `pid` is the `--pid` value, or `None` for `--parent`, whose failures all read the same.
fn bind_error_message(strings: Strings, error: BindError, pid: Option<i64>) -> String {
    match (error, pid) {
        (BindError::IsSelf, _) => strings.get(StringId::CliErrPidSelf).to_owned(),
        (BindError::NotFound, Some(pid)) => strings.format(StringId::CliErrPidNotFound, &pid.to_string()),
        (BindError::AccessDenied, Some(pid)) => strings.format(StringId::CliErrPidAccess, &pid.to_string()),
        (BindError::ParentNotFound, _) | (BindError::NotFound | BindError::AccessDenied, None) => {
            strings.get(StringId::CliErrParentNotFound).to_owned()
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn bind_errors_name_the_pid_only_when_there_is_one() {
        let strings = Strings::ENGLISH;
        let message = |error, pid| bind_error_message(strings, error, pid);
        let parent = strings.get(StringId::CliErrParentNotFound);
        assert!(message(BindError::NotFound, Some(4242)).contains("4242"));
        assert!(message(BindError::AccessDenied, Some(4242)).contains("4242"));
        assert_ne!(
            message(BindError::NotFound, Some(4242)),
            message(BindError::AccessDenied, Some(4242))
        );
        assert_eq!(
            message(BindError::IsSelf, Some(1)),
            strings.get(StringId::CliErrPidSelf)
        );
        // `--parent` has no PID to show; every failure reads as "no parent".
        assert_eq!(message(BindError::AccessDenied, None), parent);
        assert_eq!(message(BindError::ParentNotFound, None), parent);
    }

    #[test]
    fn a_name_another_program_holds_is_explained() {
        let strings = Strings::ENGLISH;
        let denied = io::Error::from_raw_os_error(5);
        assert_eq!(
            named_object_message(strings, &denied),
            strings.get(StringId::CliErrNameTaken)
        );
        let other = io::Error::from_raw_os_error(8);
        assert_eq!(
            named_object_message(strings, &other),
            strings.format(StringId::CliErrStart, &other.to_string())
        );
    }

    #[test]
    fn unreadable_until_text_is_refused_by_name() {
        let error = resolve_until(
            &UntilSpec::Text("soonish".into()),
            SystemClock.now(),
            Strings::ENGLISH,
        )
        .unwrap_err();
        assert!(error.contains("soonish"), "{error}");
    }
}
