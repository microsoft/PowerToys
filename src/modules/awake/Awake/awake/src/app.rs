//! Startup, the window procedure, and carrying out the engine's effects.
//!
//! Everything that touches application state runs on the one thread that created the window and
//! holds the keep-awake request. Other threads (the waiter, the console writer, the logger and
//! the console control handler) only post messages to it or receive values from it.

use std::cell::RefCell;
use std::ffi::OsString;
use std::os::windows::io::{AsRawHandle, OwnedHandle};
use std::sync::atomic::{AtomicU32, Ordering};
use std::time::{Duration, SystemTime};
use std::{fs, ptr};

use awake_core::cli::{self, Binding, Invocation, Plan, Request, UntilSpec};
use awake_core::duration::Human;
use awake_core::engine::{Activity, Effect, Engine, ExitReason, Save, Source};
use awake_core::execution_state::RELEASED;
use awake_core::powertoys;
use awake_core::settings::{AwakeSettings, FILE_NAME, ModeSetting, SettingsChange, SettingsDocument};
use awake_core::strings::{StringId, Strings};
use awake_core::text::{self, SessionSummary, Style};
use awake_core::time::{CivilDateTime, DateTimeOffset, LocalZone, ParsedTime, Timestamp};
use awake_core::tray_model;
use windows_sys::Win32::Foundation::WAIT_OBJECT_0;
use windows_sys::Win32::Foundation::{HWND, LPARAM, LRESULT, WPARAM};
use windows_sys::Win32::System::Threading::{GetExitCodeProcess, ResetEvent, WaitForMultipleObjects};
use windows_sys::Win32::UI::Shell::NIN_SELECT;
use windows_sys::Win32::UI::WindowsAndMessaging::{
    DefWindowProcW, DestroyWindow, ENDSESSION_CLOSEAPP, PBT_APMRESUMEAUTOMATIC, PBT_APMRESUMESUSPEND,
    PostMessageW, PostQuitMessage, WM_APP, WM_CLOSE, WM_CONTEXTMENU, WM_DESTROY, WM_DISPLAYCHANGE,
    WM_ENDSESSION, WM_POWERBROADCAST, WM_QUERYENDSESSION, WM_SETTINGCHANGE, WM_TIMER, WM_WTSSESSION_CHANGE,
    WTS_SESSION_LOCK, WTS_SESSION_UNLOCK,
};

use crate::bridge::{self, Policy, Telemetry};
use crate::platform::clock::SystemClock;
use crate::platform::console::{self, LiveOutput, Output};
use crate::platform::instance::{self, InstanceLock};
use crate::platform::messages::{
    TIMER_RELOAD, TIMER_SESSION, TIMER_TICK, TIMER_TRAY_RETRY, TIMER_WRITE, WM_APP_END, WM_CONSOLE_CTRL,
    WM_ENVIRONMENT_CHANGED, WM_TRAY, WM_WAITER,
};
use crate::platform::paths::Paths;
use crate::platform::process::{self, BindError, BoundProcess, BoundTarget};
use crate::platform::settings_store::{self, LoadError, SaveError, SettingsStore};
use crate::platform::tray::{IconKind, Tray};
use crate::platform::waiter::{WaitEvent, Waiter, WaiterSetup};
use crate::platform::{event, last_error, locale, menu, power, session, theme, wide, window};
use crate::{BUILD_ID, VERSION, logging};

/// Settings writers often touch the file several times in a row; read once they settle.
const RELOAD_DEBOUNCE_MS: u32 = 50;
const RETRY_DELAY_MS: u32 = 100;
const MAX_RELOAD_RETRIES: u32 = 5;
const MAX_WRITE_RETRIES: u32 = 20;
/// The countdown only redraws a seconds display, so Windows may batch its wake-ups with others.
const COUNTDOWN_TOLERANCE_MS: u32 = 200;
/// Session notifications can be unavailable early in a logon; try again for about a minute.
const SESSION_RETRY_MS: u32 = 2000;
const MAX_SESSION_RETRIES: u32 = 30;
/// How long a PowerToys-managed start waits for a previous instance that is still shutting down.
const INSTANCE_HANDOVER: Duration = Duration::from_secs(5);
/// `TaskbarCreated` is registered at run time; the window procedure needs it even when the app
/// is busy.
static TASKBAR_CREATED: AtomicU32 = AtomicU32::new(0);
const THEME_CHANGED: usize = 1;
const DISPLAY_CHANGED: usize = 2;
/// `NIN_SELECT | NINF_KEY`; windows-sys does not export it.
const NIN_KEYSELECT: u32 = NIN_SELECT + 1;

pub fn run() -> i32 {
    let code = start();
    // The console belongs to the shell that started Awake and outlives it.
    console::restore_mode();
    logging::flush(Duration::from_secs(1));
    // Only now, with the last log line written, may a console close or shutdown end the process.
    console::signal_shutdown_complete();
    code
}

fn start() -> i32 {
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
                &strings.format(StringId::CliErrStart, &error.to_string()),
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
    if shell_waits && (invocation.legacy || caller_waits_for_target) {
        launcher.hand_off(&invocation)
    } else {
        launcher.launch(&invocation)
    }
}

/// Everything checked before Awake commits to running: a refused start never shows an icon.
struct Prepared {
    instance: InstanceLock,
    source: Source,
    activity: Option<Activity>,
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
    fn prepare(&self, invocation: &Invocation) -> Result<Prepared, i32> {
        let handover = if invocation.plan == Plan::PowerToys || invocation.handoff_event.is_some() {
            crate::hooks::duration("POWERTOYS_AWAKE_TEST_HANDOVER_MS", INSTANCE_HANDOVER)
        } else {
            Duration::ZERO
        };
        let instance = match instance::acquire(&self.paths.instance_mutex_name(), handover) {
            Ok(Some(lock)) => lock,
            Ok(None) => return Err(self.refuse(self.strings.get(StringId::CliErrAlreadyRunning))),
            Err(error) => {
                return Err(self.refuse(&self.strings.format(StringId::CliErrStart, &error.to_string())));
            }
        };
        if bridge::policy() == Policy::Disabled {
            return Err(self.refuse(self.strings.get(StringId::CliErrPolicy)));
        }

        let clock = SystemClock;
        let now = clock.now();
        let (source, activity) = match &invocation.plan {
            Plan::PowerToys => (Source::PowerToys, None),
            Plan::Indefinite => (Source::CommandLine, Some(Activity::Indefinite)),
            Plan::For { seconds } => (
                Source::CommandLine,
                Some(Activity::Timed {
                    seconds: *seconds,
                    deadline: now.utc.saturating_add_secs(*seconds),
                }),
            ),
            Plan::Until(spec) => match resolve_until(spec, &clock, now, self.strings) {
                Ok(until) => (Source::CommandLine, Some(Activity::Expirable { until })),
                Err(message) => return Err(self.refuse(&message)),
            },
        };

        let bound = match invocation.binding {
            Binding::None => None,
            Binding::Pid(pid) => match process::bind_pid(pid) {
                Ok(bound) => Some(bound),
                Err(error) => return Err(self.refuse(&self.bind_error(error, Some(pid)))),
            },
            Binding::Parent => match process::bind_parent(self.parent) {
                Ok(bound) => Some(bound),
                Err(error) => return Err(self.refuse(&self.bind_error(error, None))),
            },
        };
        Ok(Prepared {
            instance,
            source,
            activity,
            bound,
        })
    }

    /// Validates in the caller's console, so mistakes are shown and the exit code reaches the
    /// shell, then continues in a detached process with everything already resolved: the parent
    /// for `-u` becomes an explicit `--pid`, and times become absolute.
    fn hand_off(&self, invocation: &Invocation) -> i32 {
        let prepared = match self.prepare(invocation) {
            Ok(prepared) => prepared,
            Err(code) => return code,
        };
        let summary = (!invocation.legacy && matches!(self.output, Output::Console(_)))
            .then(|| session_summary(self.strings, SystemClock, &prepared, invocation.display_on));
        let Prepared {
            instance,
            source,
            activity,
            bound,
        } = prepared;
        let mut args: Vec<String> = Vec::new();
        match activity {
            None => args.push("--use-pt-config".into()),
            Some(Activity::Timed { seconds, .. }) => args.extend(["for".into(), format!("{seconds}s")]),
            Some(Activity::Expirable { until }) => args.extend(["until".into(), until.to_json_string()]),
            Some(Activity::Indefinite | Activity::Passive) => {}
        }
        if invocation.display_on {
            args.push("--display-on".into());
        }
        if let Some(bound) = &bound {
            args.extend(["--pid".into(), bound.target.pid.to_string()]);
        }
        let new_console = bound.is_none() && source == Source::CommandLine;

        // The new process confirms through this event once its session runs, so the caller's
        // exit code still tells the truth.
        let ready_name = format!(
            "Local\\PowerToysAwakeHandoff-{}-{}{}",
            std::process::id(),
            SystemClock.now_utc().ticks(),
            self.paths.object_suffix
        );
        let ready = match event::manual_reset(Some(&ready_name)) {
            Ok(event) => event,
            Err(error) => {
                return self.refuse(&self.strings.format(StringId::CliErrStart, &error.to_string()));
            }
        };
        args.extend(["--handoff".into(), ready_name]);
        // Let the new process take the single-instance lock.
        drop(instance);
        let child = match console::hand_off(&args, new_console) {
            Ok(child) => child,
            Err(error) => {
                return self.refuse(&self.strings.format(StringId::CliErrStart, &error.to_string()));
            }
        };
        log::info!("Continuing detached: {args:?}");
        let result = wait_for_handoff(&ready, &child);
        // Held until the new process has opened the target itself, so no other process can take
        // over the PID it was given in between.
        drop(bound);
        match result {
            HandoffResult::Started | HandoffResult::Unknown => {
                if let Some(summary) = summary {
                    console::write_stdout(&text::session_header(self.strings, self.style, VERSION, &summary));
                }
                0
            }
            HandoffResult::Exited(code) => {
                log::warn!("The detached session ended right away with exit code {code}.");
                console::write_stderr(&text::error(
                    self.strings,
                    self.style,
                    &self
                        .strings
                        .format(StringId::CliErrStart, &format!("exit code {code}")),
                ));
                code
            }
        }
    }

    fn launch(self, invocation: &Invocation) -> i32 {
        let Prepared {
            instance,
            source,
            activity,
            bound,
        } = match self.prepare(invocation) {
            Ok(prepared) => prepared,
            Err(code) => return code,
        };

        let exit_name = self.paths.exit_event_name(&bridge::exit_event_name());
        // Opens the runner's event when it created it first.
        let exit_event = match event::manual_reset(Some(&exit_name)) {
            Ok(event) => event,
            Err(error) => {
                return self.refuse(&self.strings.format(StringId::CliErrStart, &error.to_string()));
            }
        };
        if source == Source::CommandLine {
            // A signal already set was meant for an Awake that has since exited: the runner sets
            // the event to stop the instance it started and keeps it open for a few seconds, and
            // this one could only take the lock once that instance let go. A PowerToys start keeps
            // the signal, because the runner may set it before Awake gets this far.
            // SAFETY: valid event handle.
            unsafe { ResetEvent(exit_event.as_raw_handle()) };
        }

        let window = match window::create(Some(window_proc)) {
            Ok(window) => window,
            Err(error) => {
                return self.refuse(&self.strings.format(StringId::CliErrStart, &error.to_string()));
            }
        };
        let taskbar_created = window::register_message(window, "TaskbarCreated");
        TASKBAR_CREATED.store(taskbar_created, Ordering::Relaxed);
        window::allow_message(window, WM_TRAY);
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
        console::install_ctrl_handler(window);

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
                return self.refuse(&self.strings.format(StringId::CliErrStart, &error.to_string()));
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
            taskbar_created,
            session_registered,
            session_retries: 0,
            exit: None,
            _instance: instance,
        });

        // SAFETY: `app` lives until after the message loop; the pointer is cleared below before
        // `app` is dropped, and the window procedure only runs on this thread.
        unsafe { window::set_user_data(window, ptr::from_ref(&app) as isize) };
        let outcome = app.borrow_mut().start(activity, invocation.display_on);
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
        app.into_inner().finish()
    }

    fn refuse(&self, message: &str) -> i32 {
        log::warn!("Not starting: {message}");
        self.telemetry.cli_command(false);
        console::write_stderr(&text::error(self.strings, self.style, message));
        1
    }

    fn bind_error(&self, error: BindError, pid: Option<i64>) -> String {
        bind_error_message(self.strings, error, pid)
    }
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

/// What `resolve_until` needs from the clock, so tests can use a fixed zone.
trait UntilZone: LocalZone {
    fn parse_regional(&self, text: &str) -> Option<CivilDateTime>;
}

impl UntilZone for SystemClock {
    fn parse_regional(&self, text: &str) -> Option<CivilDateTime> {
        SystemClock::parse_regional(*self, text)
    }
}

fn resolve_until(
    spec: &UntilSpec,
    clock: &impl UntilZone,
    now: DateTimeOffset,
    strings: Strings,
) -> Result<DateTimeOffset, String> {
    let invalid = |input: &str| strings.format(StringId::CliErrTime, input);
    let until = match spec {
        UntilSpec::Parsed(ParsedTime::Absolute(at)) => *at,
        UntilSpec::Parsed(ParsedTime::Local(local)) => clock
            .resolve(*local)
            .ok_or_else(|| invalid(&format!("{local:?}")))?,
        UntilSpec::Parsed(ParsedTime::TimeOfDay { hour, minute, second }) => {
            let today = now.local();
            let at = |date: CivilDateTime| {
                CivilDateTime::new(date.year, date.month, date.day, *hour, *minute, *second)
                    .and_then(|c| clock.resolve(c))
            };
            match at(today) {
                Some(candidate) if candidate.utc > now.utc => candidate,
                _ => {
                    let tomorrow =
                        CivilDateTime::from_timestamp(today.to_timestamp().saturating_add_secs(86_400));
                    at(tomorrow).ok_or_else(|| invalid(&format!("{hour}:{minute:02}")))?
                }
            }
        }
        UntilSpec::Text(input) => clock
            .parse_regional(input)
            .and_then(|local| clock.resolve(local))
            .ok_or_else(|| invalid(input))?,
    };
    if until.utc <= now.utc {
        return Err(strings.get(StringId::CliErrPast).to_owned());
    }
    Ok(until)
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
enum Outcome {
    Handled(LRESULT),
    Default,
    ShowMenu(i32, i32),
    Destroy,
}

/// What Awake last found at `settings.json`.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
enum SettingsFile {
    /// Not read or saved yet, or its write time could not be read.
    Unseen,
    /// Read or saved when it had this write time. Every writer changes it, so finding the same
    /// time again means a version already acted on, Awake's own save echoing back included.
    Seen(SystemTime),
    /// The last read found no file; its removal is logged once.
    Missing,
}

impl SettingsFile {
    fn seen(path: &std::path::Path) -> SettingsFile {
        write_time(path).map_or(SettingsFile::Unseen, SettingsFile::Seen)
    }
}

#[derive(Clone, Copy, Debug)]
struct ExitPlan {
    code: i32,
    reason: Farewell,
}

#[derive(Clone, Copy, Debug)]
enum Farewell {
    Stopped,
    Finished,
    TargetExited,
    Failed,
}

struct App {
    window: HWND,
    strings: Strings,
    right_to_left: bool,
    clock: SystemClock,
    engine: Engine,
    tray: Tray,
    telemetry: Telemetry,
    store: Option<SettingsStore>,
    pending_changes: Vec<SettingsChange>,
    write_retries: u32,
    reload_retries: u32,
    settings_file: SettingsFile,
    waiter: Option<Waiter>,
    bound: Option<BoundTarget>,
    live: Option<LiveOutput>,
    redirected: bool,
    style: Style,
    taskbar_created: u32,
    /// Lock notifications arrive; until then the lock state is the one read at startup.
    session_registered: bool,
    session_retries: u32,
    exit: Option<ExitPlan>,
    _instance: InstanceLock,
}

impl App {
    fn start(&mut self, activity: Option<Activity>, display_on: bool) -> Outcome {
        let effects = match activity {
            Some(activity) => self.engine.start(activity, display_on),
            None => self.load_initial_settings(),
        };
        let outcome = self.apply(effects);
        self.refresh_icon();
        if self.engine.source() == Source::CommandLine {
            let header = text::session_header(self.strings, self.style, VERSION, &self.summary());
            match &self.live {
                Some(live) => live.print(header),
                None if self.redirected => console::write_stdout(&header),
                None => {}
            }
            self.update_status();
        }
        outcome
    }

    fn load_initial_settings(&mut self) -> Vec<Effect> {
        let Some(store) = &self.store else {
            return Vec::new();
        };
        let now = self.clock.now();
        let version = write_time(store.path());
        match store.read() {
            Ok(document) => match document.settings(&self.clock) {
                Ok(settings) => return self.apply_loaded(&settings, version),
                Err(error) => log::error!("Ignoring the settings file: {error}"),
            },
            Err(LoadError::Missing) => {
                log::info!("No settings file yet; creating one with defaults.");
                match store.write(&SettingsDocument::scaffold(now)) {
                    Ok(()) => self.settings_file = SettingsFile::seen(store.path()),
                    Err(error) => log::error!("Could not create the settings file: {error}"),
                }
            }
            // Never replace a file that exists but cannot be read: other programs own it too.
            Err(error) => log::error!("Ignoring the settings file: {error}"),
        }
        Vec::new()
    }

    /// Hands settings read from the file to the engine, at startup and on every reload alike.
    /// `version` is the file's write time, taken before reading it.
    fn apply_loaded(&mut self, settings: &AwakeSettings, version: Option<SystemTime>) -> Vec<Effect> {
        if let ModeSetting::Unknown(value) = settings.mode {
            log::warn!("Ignoring unknown mode {value} in the settings file.");
        }
        let save = match version {
            Some(time) if self.settings_file == SettingsFile::Seen(time) => Save::Known,
            _ => Save::New,
        };
        self.settings_file = version.map_or(SettingsFile::Unseen, SettingsFile::Seen);
        self.engine.apply_saved_settings(settings, self.clock.now(), save)
    }

    fn on_message(&mut self, message: u32, wparam: WPARAM, lparam: LPARAM) -> Outcome {
        match message {
            WM_WAITER => match WaitEvent::from_wparam(wparam) {
                Some(WaitEvent::ExitRequested) => {
                    log::info!("Exit event signaled.");
                    self.request_exit(0, Farewell::Stopped)
                }
                Some(WaitEvent::ProcessExited) => {
                    log::info!("The bound process exited.");
                    self.request_exit(0, Farewell::TargetExited)
                }
                Some(WaitEvent::DeadlineReached) => self.tick(Engine::deadline_fired),
                Some(WaitEvent::SettingsChanged) => {
                    if window::set_timer(self.window, TIMER_RELOAD, RELOAD_DEBOUNCE_MS) {
                        Outcome::Handled(0)
                    } else {
                        self.reload_settings()
                    }
                }
                None => Outcome::Handled(0),
            },
            WM_TRAY => match window::loword(lparam as usize) {
                NIN_SELECT | NIN_KEYSELECT | WM_CONTEXTMENU => {
                    let (x, y) = window::point_from_wparam(wparam);
                    Outcome::ShowMenu(x, y)
                }
                _ => Outcome::Handled(0),
            },
            WM_TIMER => match wparam {
                TIMER_TICK => self.tick(Engine::tick),
                TIMER_RELOAD => {
                    window::kill_timer(self.window, TIMER_RELOAD);
                    self.reload_settings()
                }
                TIMER_WRITE => {
                    window::kill_timer(self.window, TIMER_WRITE);
                    self.write_pending();
                    Outcome::Handled(0)
                }
                TIMER_TRAY_RETRY => {
                    self.tray.retry();
                    Outcome::Handled(0)
                }
                TIMER_SESSION => {
                    window::kill_timer(self.window, TIMER_SESSION);
                    self.register_session()
                }
                _ => Outcome::Default,
            },
            WM_POWERBROADCAST => {
                // Only resume matters. PBT_APMPOWERSTATUSCHANGE also arrives with every few percent
                // of battery, and a change of power source leaves the thread's request in force.
                if matches!(wparam as u32, PBT_APMRESUMEAUTOMATIC | PBT_APMRESUMESUSPEND) {
                    log::info!("Resumed ({wparam:#x}); asserting the request again.");
                    let effects = self.engine.power_changed();
                    self.apply(effects);
                }
                Outcome::Handled(1)
            }
            WM_WTSSESSION_CHANGE => {
                let locked = match u32::try_from(wparam) {
                    Ok(WTS_SESSION_LOCK) => Some(true),
                    Ok(WTS_SESSION_UNLOCK) => Some(false),
                    _ => None,
                };
                if let Some(locked) = locked {
                    log::info!("Session {}.", if locked { "locked" } else { "unlocked" });
                    let effects = self.engine.set_screen_locked(locked);
                    self.apply(effects);
                    if !locked {
                        // Countdown redraws were skipped while locked.
                        self.refresh_icon();
                    }
                }
                Outcome::Handled(0)
            }
            WM_SETTINGCHANGE | WM_DISPLAYCHANGE | WM_ENVIRONMENT_CHANGED => {
                // Windows broadcasts WM_SETTINGCHANGE for every kind of setting, often several
                // times per change; only act on the ones that affect Awake.
                let change = environment_change(message, wparam, lparam);
                if change & THEME_CHANGED != 0 {
                    theme::apply_system_theme();
                }
                if change & DISPLAY_CHANGED != 0 {
                    self.tray.display_changed();
                }
                if message == WM_ENVIRONMENT_CHANGED {
                    Outcome::Handled(0)
                } else {
                    Outcome::Default
                }
            }
            WM_CONSOLE_CTRL => {
                log::info!("Console control event {wparam}.");
                self.request_exit(0, Farewell::Stopped)
            }
            // Closing the window (taskkill, the installer) is an exit, never a silent headless run.
            WM_CLOSE => {
                log::info!("Asked to close.");
                self.request_exit(0, Farewell::Stopped)
            }
            WM_QUERYENDSESSION => Outcome::Handled(1),
            WM_ENDSESSION => {
                if wparam == 0 {
                    return Outcome::Handled(0);
                }
                if lparam as u32 & ENDSESSION_CLOSEAPP != 0 {
                    // The Restart Manager, for an installer or servicing: the session goes on,
                    // and Windows expects Awake to close itself.
                    log::info!("Asked to close so files can be updated.");
                } else {
                    log::info!("Windows is signing out or shutting down.");
                }
                // Windows may end the process as soon as this returns, so clean up right here.
                release_request();
                self.tray.remove();
                logging::flush(Duration::from_millis(500));
                self.request_exit(0, Farewell::Stopped)
            }
            WM_DESTROY => {
                // While the window still exists, so Explorer can match the icon to its owner.
                self.tray.remove();
                if self.session_registered {
                    session::unregister(self.window);
                    self.session_registered = false;
                }
                // SAFETY: ends this thread's message loop.
                unsafe { PostQuitMessage(0) };
                Outcome::Handled(0)
            }
            m if m == self.taskbar_created && m != 0 => {
                log::info!("Explorer restarted; adding the icon again.");
                self.tray.taskbar_created();
                if !self.session_registered {
                    // A new shell is a good moment to try again for lock notifications.
                    self.session_retries = 0;
                    return self.register_session();
                }
                Outcome::Handled(0)
            }
            _ => Outcome::Default,
        }
    }

    /// Retries asking for lock notifications, then catches up with a lock that happened
    /// meanwhile, so the display is not held on through the lock screen.
    fn register_session(&mut self) -> Outcome {
        if self.session_registered {
            return Outcome::Handled(0);
        }
        match session::register(self.window) {
            Ok(()) => {
                log::info!("Lock notifications are available.");
                self.session_registered = true;
                self.session_retries = 0;
                let effects = self.engine.set_screen_locked(session::is_locked());
                self.apply(effects)
            }
            Err(error) if self.session_retries < MAX_SESSION_RETRIES => {
                self.session_retries += 1;
                log::debug!(
                    "Lock notifications still unavailable ({error}); retry {}.",
                    self.session_retries
                );
                if !window::set_timer(self.window, TIMER_SESSION, SESSION_RETRY_MS) {
                    log::error!("Could not schedule the retry: {}", last_error());
                }
                Outcome::Handled(0)
            }
            Err(error) => {
                log::error!("No lock notifications ({error}); the display request stays while locked.");
                Outcome::Handled(0)
            }
        }
    }

    fn menu_model(&self) -> (Vec<tray_model::MenuItem>, Vec<awake_core::settings::Preset>) {
        let items = tray_model::menu(&self.engine, self.strings, self.bound.is_some());
        (items, self.engine.presets().to_vec())
    }

    fn on_menu_command(&mut self, id: u16, presets: &[awake_core::settings::Preset]) -> Outcome {
        match tray_model::command_for(id, presets) {
            Some(command) => {
                log::info!("Menu: {command:?}");
                let effects = self.engine.tray(command, self.clock.now());
                self.apply(effects)
            }
            None => Outcome::Handled(0),
        }
    }

    fn tick(&mut self, input: fn(&mut Engine, Timestamp) -> Vec<Effect>) -> Outcome {
        let mut effects = input(&mut self.engine, self.clock.now_utc());
        if self.session_registered
            && self.engine.screen_locked()
            && suppress_while_locked(&effects, self.engine.activity())
        {
            // Nobody sees the notification area on the lock screen; unlocking redraws the icon.
            effects.clear();
        }
        let outcome = self.apply(effects);
        self.update_status();
        outcome
    }

    fn reload_settings(&mut self) -> Outcome {
        if let Some(waiter) = &self.waiter {
            waiter.settings_handled();
        }
        let Some(store) = &self.store else {
            return Outcome::Handled(0);
        };
        // Taken before the content: a save landing in between then looks known, and its own
        // change notification brings it back as new.
        let version = write_time(store.path());
        // Unsaved changes from the menu win over what the file says until they are written.
        let loaded = match store.read() {
            Ok(mut document) => {
                document.apply(&self.pending_changes);
                document.settings(&self.clock).map_err(|e| e.to_string())
            }
            // Not a writer halfway through, so no retries. Saving from the menu creates the file
            // again, as at startup; until then nothing says the mode should change.
            Err(LoadError::Missing) => {
                self.reload_retries = 0;
                if self.settings_file != SettingsFile::Missing {
                    self.settings_file = SettingsFile::Missing;
                    log::info!(
                        "The settings file was removed; keeping the current mode until it is written again."
                    );
                }
                return Outcome::Handled(0);
            }
            Err(error) => Err(error.to_string()),
        };
        match loaded {
            Ok(settings) => {
                self.reload_retries = 0;
                let effects = self.apply_loaded(&settings, version);
                if !effects.is_empty() {
                    log::info!("Settings changed: {effects:?}");
                }
                self.apply(effects)
            }
            // A writer may be halfway through; look again shortly, then give up quietly.
            Err(error) if self.reload_retries < MAX_RELOAD_RETRIES => {
                self.reload_retries += 1;
                log::debug!(
                    "Settings not readable yet ({error}); retry {}.",
                    self.reload_retries
                );
                if window::set_timer(self.window, TIMER_RELOAD, RETRY_DELAY_MS) {
                    Outcome::Handled(0)
                } else {
                    self.reload_retries = 0;
                    log::warn!("Keeping the current mode; could not schedule another read: {error}");
                    Outcome::Handled(0)
                }
            }
            Err(error) => {
                self.reload_retries = 0;
                log::warn!("Keeping the current mode; the settings file is unreadable: {error}");
                Outcome::Handled(0)
            }
        }
    }

    fn apply(&mut self, effects: Vec<Effect>) -> Outcome {
        let mut outcome = Outcome::Handled(0);
        for effect in effects {
            let result = match effect {
                Effect::SetExecutionState(flags) => {
                    if power::set_execution_state(flags) {
                        log::info!("Execution state set to {flags:#x}.");
                        Outcome::Handled(0)
                    } else {
                        log::error!("Windows rejected execution state {flags:#x}: {}", last_error());
                        // Whatever was requested before is still in force; drop it explicitly.
                        power::set_execution_state(RELEASED);
                        // The fallback describes the whole new state; the rest of this batch is stale.
                        let fallback = self.engine.execution_state_failed();
                        return self.apply(fallback);
                    }
                }
                Effect::ScheduleDeadline(deadline) => {
                    if let Some(waiter) = &self.waiter {
                        waiter.set_deadline(deadline);
                    }
                    Outcome::Handled(0)
                }
                Effect::Countdown(on) => {
                    if !on {
                        window::kill_timer(self.window, TIMER_TICK);
                    } else if !window::set_coalescable_timer(
                        self.window,
                        TIMER_TICK,
                        1000,
                        COUNTDOWN_TOLERANCE_MS,
                    ) {
                        // Only the display depends on it; the session ends on the waiter's timer.
                        log::warn!("No live countdown: {}", last_error());
                    }
                    Outcome::Handled(0)
                }
                Effect::RefreshIcon => {
                    self.refresh_icon();
                    Outcome::Handled(0)
                }
                Effect::Persist(changes) => {
                    self.pending_changes.extend(changes);
                    self.write_pending();
                    Outcome::Handled(0)
                }
                Effect::ModeEntered(mode) => {
                    log::info!("Entered {mode:?}.");
                    self.telemetry.mode_entered(mode);
                    Outcome::Handled(0)
                }
                Effect::Exit(reason) => match reason {
                    ExitReason::Requested => self.request_exit(0, Farewell::Stopped),
                    ExitReason::TimeUp => self.request_exit(0, Farewell::Finished),
                    ExitReason::PowerRequestFailed => self.request_exit(1, Farewell::Failed),
                },
            };
            if result == Outcome::Destroy {
                outcome = Outcome::Destroy;
            }
        }
        outcome
    }

    /// Re-reads the file and applies the pending changes on top, so edits other programs made
    /// in the meantime are kept.
    ///
    /// This runs on the window thread on purpose, flush to disk included: the file is small,
    /// writes are rare and follow a user's choice or a session ending, and the runner and the
    /// Settings app read the result, so it has to survive a power cut. Keeping it here also keeps
    /// the pending changes and retries in one place. See `settings_store` for the details.
    fn write_pending(&mut self) {
        let Some(store) = &self.store else {
            self.pending_changes.clear();
            return;
        };
        if self.pending_changes.is_empty() {
            return;
        }
        let now = self.clock.now();
        let mut document = match store.read() {
            Ok(document) => document,
            Err(LoadError::Missing) => SettingsDocument::scaffold(now),
            // Probably another writer mid-write; never replace a file others own with defaults.
            Err(error) if self.write_retries < MAX_WRITE_RETRIES => {
                self.write_retries += 1;
                log::debug!(
                    "Settings not readable before saving ({error}); retry {}.",
                    self.write_retries
                );
                self.schedule_write_retry();
                return;
            }
            Err(error) => {
                log::error!(
                    "Not saving {:?}: the settings file stays unreadable: {error}",
                    self.pending_changes
                );
                self.pending_changes.clear();
                self.write_retries = 0;
                return;
            }
        };
        if !document.apply(&self.pending_changes) {
            self.pending_changes.clear();
            return;
        }
        match store.write(&document) {
            Ok(()) => {
                log::info!("Saved {:?}.", self.pending_changes);
                self.settings_file = SettingsFile::seen(store.path());
                self.pending_changes.clear();
                self.write_retries = 0;
            }
            Err(SaveError::Busy) if self.write_retries < MAX_WRITE_RETRIES => {
                self.write_retries += 1;
                self.schedule_write_retry();
            }
            Err(error) => {
                log::error!("Could not save settings: {error}");
                self.pending_changes.clear();
                self.write_retries = 0;
            }
        }
    }

    /// Without a timer nothing would ever write the pending changes, so give up on them visibly
    /// rather than leave them queued.
    fn schedule_write_retry(&mut self) {
        if !window::set_timer(self.window, TIMER_WRITE, RETRY_DELAY_MS) {
            log::error!(
                "Not saving {:?}: could not schedule a retry: {}",
                self.pending_changes,
                last_error()
            );
            self.pending_changes.clear();
            self.write_retries = 0;
        }
    }

    fn refresh_icon(&mut self) {
        let kind = match self.engine.activity() {
            Activity::Passive => IconKind::Passive,
            Activity::Indefinite => IconKind::Indefinite,
            Activity::Timed { .. } => IconKind::Timed,
            Activity::Expirable { .. } => IconKind::Expirable,
        };
        // The PID line only ever appeared for command-line sessions.
        let pid = match self.engine.source() {
            Source::CommandLine => self.bound.as_ref().map(|bound| bound.pid),
            Source::PowerToys => None,
        };
        let clock = self.clock;
        let tooltip = tray_model::tooltip(&self.engine, self.strings, clock.now_utc(), pid, &|at| {
            clock.format_expiry(at)
        });
        self.tray.show(kind, &tooltip);
    }

    fn summary(&self) -> SessionSummary {
        session_summary_for(
            self.strings,
            self.clock,
            self.engine.activity(),
            self.engine.keep_display_on(),
            self.bound.clone(),
        )
    }

    fn update_status(&self) {
        let Some(live) = &self.live else { return };
        if self.exit.is_some() {
            return;
        }
        let now = self.clock.now_utc();
        let remaining = self
            .engine
            .activity()
            .deadline()
            .map(|deadline| now.secs_until(deadline));
        live.status(text::status_line(self.strings, self.style, remaining));
    }

    fn request_exit(&mut self, code: i32, reason: Farewell) -> Outcome {
        crate::hooks::hang_on_exit();
        if self.exit.is_none() {
            self.exit = Some(ExitPlan { code, reason });
        }
        Outcome::Destroy
    }

    fn finish(mut self) -> i32 {
        if let Some(waiter) = self.waiter.take() {
            waiter.stop();
        }
        // The request ends with this thread anyway; releasing it explicitly makes that visible
        // in the log and to anyone inspecting `powercfg /requests` during shutdown.
        release_request();
        if !self.pending_changes.is_empty() {
            // A save waiting out a busy file would die with the window's retry timer, and the next
            // start would read a session that has already ended. One last try, without retries.
            self.write_retries = MAX_WRITE_RETRIES;
            self.write_pending();
        }
        let exit = self.exit.unwrap_or(ExitPlan {
            code: 0,
            reason: Farewell::Stopped,
        });
        let farewell = match exit.reason {
            Farewell::Stopped => text::farewell(self.strings, self.style, StringId::CliStopped),
            Farewell::Finished => text::farewell(self.strings, self.style, StringId::CliFinished),
            Farewell::TargetExited => text::farewell(self.strings, self.style, StringId::CliTargetExited),
            Farewell::Failed => {
                format!(
                    "{}{}",
                    self.style.clear_line(),
                    text::error(self.strings, self.style, self.strings.get(StringId::CliErrPower))
                )
            }
        };
        match self.live.take() {
            Some(live) => live.finish(format!("{farewell}\n"), Duration::from_secs(1)),
            // A pipe never had a live status line, so there is nothing to clear first.
            None if self.redirected && self.engine.source() == Source::CommandLine => {
                console::write_stdout(farewell.trim_start_matches(['\r', ' ']));
            }
            None => {}
        }
        // `run` flushes this line before letting a console close end the process.
        log::info!("Exiting with code {}.", exit.code);
        exit.code
    }
}

/// Drops whatever this thread asked Windows for, and says so in the log.
fn release_request() {
    if power::set_execution_state(RELEASED) {
        log::info!("Execution state released.");
    } else {
        log::error!("Could not release the execution state: {}", last_error());
    }
}

fn write_time(path: &std::path::Path) -> Option<SystemTime> {
    fs::metadata(path).and_then(|metadata| metadata.modified()).ok()
}

fn session_summary(
    strings: Strings,
    clock: SystemClock,
    prepared: &Prepared,
    display_on: bool,
) -> SessionSummary {
    let activity = prepared.activity.unwrap_or(Activity::Indefinite);
    let bound = prepared.bound.as_ref().map(|b| b.target.clone());
    session_summary_for(strings, clock, activity, display_on, bound)
}

fn session_summary_for(
    strings: Strings,
    clock: SystemClock,
    activity: Activity,
    display_on: bool,
    bound: Option<BoundTarget>,
) -> SessionSummary {
    let headline = match activity {
        Activity::Timed { seconds, .. } => {
            strings.format(StringId::CliStatusTimed, &Human(seconds).to_string())
        }
        Activity::Expirable { until } => {
            strings.format(StringId::CliStatusUntil, &clock.format_expiry(&until))
        }
        Activity::Indefinite | Activity::Passive => strings.get(StringId::CliStatusIndefinite).to_owned(),
    };
    let bound_to = bound.map(|BoundTarget { pid, image_name }| {
        let process = strings.format(StringId::CliProcess, &pid.to_string());
        match image_name {
            Some(name) => format!("{process} ({name})"),
            None => process,
        }
    });
    SessionSummary {
        headline,
        display_on,
        bound_to,
    }
}

enum HandoffResult {
    Started,
    Exited(i32),
    /// Neither happened in time; the session is most likely running.
    Unknown,
}

fn wait_for_handoff(ready: &OwnedHandle, child: &OwnedHandle) -> HandoffResult {
    let handles = [ready.as_raw_handle(), child.as_raw_handle()];
    // SAFETY: both handles are open for the duration of the call.
    let result = unsafe { WaitForMultipleObjects(2, handles.as_ptr(), 0, 15_000) };
    match result {
        r if r == WAIT_OBJECT_0 => HandoffResult::Started,
        r if r == WAIT_OBJECT_0 + 1 => {
            let mut code = 1u32;
            // SAFETY: the child has exited; its handle is valid.
            unsafe { GetExitCodeProcess(child.as_raw_handle(), &mut code) };
            HandoffResult::Exited(code as i32)
        }
        _ => HandoffResult::Unknown,
    }
}

unsafe extern "system" fn window_proc(window: HWND, message: u32, wparam: WPARAM, lparam: LPARAM) -> LRESULT {
    let data = window::user_data(window) as *const RefCell<App>;
    if data.is_null() {
        // SAFETY: default handling for messages that arrive before or after the app exists.
        return unsafe { DefWindowProcW(window, message, wparam, lparam) };
    }
    // SAFETY: set in `launch` to a RefCell that outlives the message loop and is cleared after it.
    let cell = unsafe { &*data };
    let outcome = match cell.try_borrow_mut() {
        Ok(mut app) => app.on_message(message, wparam, lparam),
        // Windows re-entered the procedure from inside a call made while handling another
        // message (a cross-process call such as Shell_NotifyIcon dispatches sent messages).
        Err(_) => return reentered(window, message, wparam, lparam),
    };
    match outcome {
        Outcome::Handled(result) => result,
        // SAFETY: default handling.
        Outcome::Default => unsafe { DefWindowProcW(window, message, wparam, lparam) },
        Outcome::ShowMenu(x, y) => {
            show_menu(cell, window, x, y);
            0
        }
        Outcome::Destroy => {
            // SAFETY: our own window; WM_DESTROY re-enters this procedure after the borrow ended.
            unsafe { DestroyWindow(window) };
            0
        }
    }
}

/// What a settings or display broadcast means for Awake, as `THEME_CHANGED | DISPLAY_CHANGED`.
fn environment_change(message: u32, wparam: WPARAM, lparam: LPARAM) -> usize {
    match message {
        WM_DISPLAYCHANGE => DISPLAY_CHANGED,
        // lParam names the changed section; switching between light and dark is this one.
        WM_SETTINGCHANGE if lparam != 0 => {
            // SAFETY: for WM_SETTINGCHANGE a non-null lParam is a NUL-terminated string, valid
            // while the message is being handled.
            let theme = unsafe { wide::nul_terminated_eq(lparam as *const u16, "ImmersiveColorSet") };
            if theme { THEME_CHANGED } else { 0 }
        }
        WM_ENVIRONMENT_CHANGED => wparam,
        _ => 0,
    }
}

/// Handles a message that arrived while the app was busy. Nothing that ends the window or
/// changes the session may fall through to default handling, or Awake could end up without a
/// window while still holding the keep-awake request.
fn reentered(window: HWND, message: u32, wparam: WPARAM, lparam: LPARAM) -> LRESULT {
    let repost = |result: LRESULT| {
        // SAFETY: re-posting to our own window, to be handled once the app is free.
        unsafe { PostMessageW(window, message, wparam, lparam) };
        result
    };
    match message {
        WM_DESTROY => {
            // While the window still exists, as in `App::on_message`.
            crate::platform::tray::remove_icon(window);
            // SAFETY: ends this thread's message loop; finish() cleans up afterwards.
            unsafe { PostQuitMessage(0) };
            0
        }
        WM_QUERYENDSESSION | WM_POWERBROADCAST => repost(1),
        // Windows may end the process as soon as this returns, so clean up right here, then
        // close once the app is free: after a Restart Manager request the session goes on.
        WM_ENDSESSION if wparam != 0 => {
            release_request();
            crate::platform::tray::remove_icon(window);
            logging::flush(Duration::from_millis(500));
            // SAFETY: posting to our own window.
            unsafe { PostMessageW(window, WM_CLOSE, 0, 0) };
            0
        }
        // The settings text in lParam is only valid during the call, so post what it means instead.
        WM_SETTINGCHANGE | WM_DISPLAYCHANGE => {
            let change = environment_change(message, wparam, lparam);
            if change != 0 {
                // SAFETY: posting to our own window.
                unsafe { PostMessageW(window, WM_ENVIRONMENT_CHANGED, change, 0) };
            }
            0
        }
        WM_CLOSE | WM_WTSSESSION_CHANGE => repost(0),
        m if (WM_APP..WM_APP_END).contains(&m) || m == TASKBAR_CREATED.load(Ordering::Relaxed) => repost(0),
        // SAFETY: default handling for everything else.
        _ => unsafe { DefWindowProcW(window, message, wparam, lparam) },
    }
}

/// Runs the menu's modal loop without holding the app borrow, so timers and waiter messages
/// keep flowing while it is open.
fn show_menu(cell: &RefCell<App>, window: HWND, x: i32, y: i32) {
    let Ok((items, presets, right_to_left)) = cell.try_borrow().map(|app| {
        let (items, presets) = app.menu_model();
        (items, presets, app.right_to_left)
    }) else {
        return;
    };
    if items.is_empty() {
        return;
    }
    let Some(popup) = menu::build(&items, right_to_left) else {
        log::error!("Could not build the context menu: {}", last_error());
        return;
    };
    let command = menu::track(&popup, window, x, y, right_to_left);
    drop(popup);
    let outcome = match cell.try_borrow_mut() {
        Ok(mut app) => {
            app.tray.return_focus();
            if command == 0 {
                Outcome::Handled(0)
            } else {
                app.on_menu_command(command, &presets)
            }
        }
        Err(_) => return,
    };
    if outcome == Outcome::Destroy {
        // SAFETY: our own window.
        unsafe { DestroyWindow(window) };
    }
}

/// Whether a timer's effects are only the countdown redraw, which can wait until unlock. Anything
/// else (a session ending at its deadline, say) changes the icon for good and must go through.
fn suppress_while_locked(effects: &[Effect], activity: Activity) -> bool {
    matches!(activity, Activity::Timed { .. }) && effects == [Effect::RefreshIcon]
}

#[cfg(test)]
mod tests {
    use super::*;
    use awake_core::time::FixedZone;

    impl UntilZone for FixedZone {
        fn parse_regional(&self, _: &str) -> Option<CivilDateTime> {
            None
        }
    }

    fn local(zone: FixedZone, hour: u8, minute: u8, second: u8) -> DateTimeOffset {
        zone.resolve(CivilDateTime::new(2026, 3, 28, hour, minute, second).unwrap())
            .unwrap()
    }

    #[test]
    fn until_a_time_of_day_means_the_next_one() {
        let zone = FixedZone(60);
        let now = local(zone, 10, 30, 15);
        let time_of_day = |hour, minute| {
            UntilSpec::Parsed(ParsedTime::TimeOfDay {
                hour,
                minute,
                second: 0,
            })
        };
        let later = resolve_until(&time_of_day(18, 0), &zone, now, Strings::ENGLISH).unwrap();
        assert_eq!(later, local(zone, 18, 0, 0));
        let earlier = resolve_until(&time_of_day(10, 30), &zone, now, Strings::ENGLISH).unwrap();
        let tomorrow = zone
            .resolve(CivilDateTime::new(2026, 3, 29, 10, 30, 0).unwrap())
            .unwrap();
        assert_eq!(earlier, tomorrow);
    }

    #[test]
    fn past_absolute_times_are_refused() {
        let strings = Strings::ENGLISH;
        let past = FixedZone(0)
            .resolve(CivilDateTime::new(2000, 1, 1, 0, 0, 0).unwrap())
            .unwrap();
        let error = resolve_until(
            &UntilSpec::Parsed(ParsedTime::Absolute(past)),
            &SystemClock,
            SystemClock.now(),
            strings,
        )
        .unwrap_err();
        assert_eq!(error, "That time has already passed.");
    }

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
    fn unparseable_regional_text_is_refused() {
        let error = resolve_until(
            &UntilSpec::Text("soonish".into()),
            &SystemClock,
            SystemClock.now(),
            Strings::ENGLISH,
        )
        .unwrap_err();
        assert!(error.contains("soonish"));
    }

    #[test]
    fn a_plain_countdown_tick_waits_for_unlock() {
        let mut engine = Engine::new(Source::PowerToys, Vec::new(), true);
        let deadline = Timestamp::from_ticks(0).saturating_add_secs(60);
        engine.start(
            Activity::Timed {
                seconds: 60,
                deadline,
            },
            false,
        );
        let effects = engine.tick(Timestamp::from_ticks(0).saturating_add_secs(30));
        assert!(suppress_while_locked(&effects, engine.activity()));
    }

    #[test]
    fn an_expiry_while_locked_still_redraws_the_icon() {
        let mut engine = Engine::new(Source::PowerToys, Vec::new(), true);
        let deadline = Timestamp::from_ticks(0).saturating_add_secs(60);
        engine.start(
            Activity::Timed {
                seconds: 60,
                deadline,
            },
            false,
        );
        let effects = engine.deadline_fired(deadline);
        assert!(effects.contains(&Effect::RefreshIcon));
        assert_eq!(engine.activity(), Activity::Passive);
        assert!(!suppress_while_locked(&effects, engine.activity()));
    }
}
