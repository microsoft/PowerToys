//! The running session: what each window message means for it, and carrying out the engine's
//! effects.
//!
//! Everything that touches application state runs on the one thread that created the window and
//! holds the keep-awake request. Other threads (the waiter, the console writer, the logger and
//! the console control handler) only post messages to it or receive values from it.
//!
//! `launch` gets a session to this point, `settings_file` keeps a PowerToys session in step with
//! `settings.json`, and `window_proc` routes messages here.

mod launch;
mod settings_file;
mod window_proc;

use std::time::Duration;

use awake_core::duration::Human;
use awake_core::engine::{Activity, Effect, Engine, ExitReason, Source};
use awake_core::execution_state::RELEASED;
use awake_core::settings::{Preset, SettingsChange};
use awake_core::strings::{StringId, Strings};
use awake_core::text::{self, SessionSummary, Style};
use awake_core::time::Timestamp;
use awake_core::tray_model;
use windows_sys::Win32::Foundation::{HWND, LPARAM, LRESULT, WPARAM};
use windows_sys::Win32::UI::Shell::NIN_SELECT;
use windows_sys::Win32::UI::WindowsAndMessaging::{
    ENDSESSION_CLOSEAPP, PBT_APMRESUMEAUTOMATIC, PBT_APMRESUMESUSPEND, PostQuitMessage, WM_CLOSE,
    WM_CONTEXTMENU, WM_DESTROY, WM_DISPLAYCHANGE, WM_ENDSESSION, WM_POWERBROADCAST, WM_QUERYENDSESSION,
    WM_SETTINGCHANGE, WM_TIMER, WM_WTSSESSION_CHANGE, WTS_SESSION_LOCK, WTS_SESSION_UNLOCK,
};

use self::settings_file::SettingsFile;
use self::window_proc::{DISPLAY_CHANGED, THEME_CHANGED};
use crate::bridge::Telemetry;
use crate::platform::clock::SystemClock;
use crate::platform::console::{self, LiveOutput};
use crate::platform::instance::InstanceLock;
use crate::platform::messages::{
    TIMER_RELOAD, TIMER_SESSION, TIMER_TICK, TIMER_TRAY_RETRY, TIMER_WRITE, WM_CONSOLE_CTRL,
    WM_ENVIRONMENT_CHANGED, WM_TRAY, WM_WAITER,
};
use crate::platform::process::BoundTarget;
use crate::platform::settings_store::SettingsStore;
use crate::platform::tray::{IconKind, Tray};
use crate::platform::waiter::{WaitEvent, Waiter};
use crate::platform::{console_ctrl, last_error, power, session, theme, window};
use crate::{VERSION, log_lines, logging};

/// The countdown only redraws a seconds display, so Windows may batch its wake-ups with others.
const COUNTDOWN_TOLERANCE_MS: u32 = 200;
/// Session notifications can be unavailable early in a logon; try again for about a minute.
const SESSION_RETRY_MS: u32 = 2000;
const MAX_SESSION_RETRIES: u32 = 30;
/// `NIN_SELECT | NINF_KEY`; windows-sys does not export it.
const NIN_KEYSELECT: u32 = NIN_SELECT + 1;

pub fn run() -> i32 {
    let code = launch::start();
    // The console belongs to the shell that started Awake and outlives it.
    console::restore_mode();
    logging::flush(Duration::from_secs(1));
    // Only now, with the last log line written, may a console close or shutdown end the process.
    console_ctrl::signal_shutdown_complete();
    code
}

/// What a session starts from.
#[derive(Clone, Copy, Debug)]
enum Start {
    /// Follow `settings.json`, as the runner asks.
    PowerToys,
    /// A command-line session, with its times already resolved.
    CommandLine(Activity),
}

impl Start {
    fn source(self) -> Source {
        match self {
            Start::PowerToys => Source::PowerToys,
            Start::CommandLine(_) => Source::CommandLine,
        }
    }
}

/// What the window procedure does after a message. Dropping one could drop a `Destroy`, leaving
/// the window alive after the session asked to exit.
#[must_use]
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
enum Outcome {
    Handled(LRESULT),
    Default,
    ShowMenu(i32, i32),
    Destroy,
}

impl Outcome {
    /// `Destroy` stays; anything else becomes the message's own `result`.
    fn or_handled(self, result: LRESULT) -> Outcome {
        match self {
            Outcome::Destroy => Outcome::Destroy,
            _ => Outcome::Handled(result),
        }
    }
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
    /// Whether `WM_WTSSESSION_CHANGE` is registered; until it is, the lock state is the one read at
    /// startup.
    session_registered: bool,
    session_retries: u32,
    /// Why the session ends, once something asked it to; the first reason wins.
    exit: Option<ExitReason>,
    _instance: InstanceLock,
}

impl App {
    fn start(&mut self, start: Start, display_on: bool) -> Outcome {
        let effects = match start {
            Start::CommandLine(activity) => self.engine.start(activity, display_on),
            Start::PowerToys => self.load_initial_settings(),
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

    fn on_message(&mut self, message: u32, wparam: WPARAM, lparam: LPARAM) -> Outcome {
        match message {
            WM_WAITER => match WaitEvent::from_wparam(wparam) {
                Some(WaitEvent::ExitRequested) => {
                    log::info!("{}", log_lines::EXIT_EVENT_SIGNALED);
                    self.request_exit(ExitReason::Requested)
                }
                Some(WaitEvent::ProcessExited) => {
                    log::info!("The bound process exited.");
                    self.request_exit(ExitReason::TargetExited)
                }
                Some(WaitEvent::DeadlineReached) => self.tick(Engine::deadline_fired),
                Some(WaitEvent::SettingsChanged) => self.settings_changed(),
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
                    return self.apply(effects).or_handled(1);
                }
                Outcome::Handled(1)
            }
            WM_WTSSESSION_CHANGE => {
                let locked = match u32::try_from(wparam) {
                    Ok(WTS_SESSION_LOCK) => Some(true),
                    Ok(WTS_SESSION_UNLOCK) => Some(false),
                    _ => None,
                };
                let Some(locked) = locked else {
                    return Outcome::Handled(0);
                };
                log::info!("Session {}.", if locked { "locked" } else { "unlocked" });
                let effects = self.engine.set_screen_locked(locked);
                let outcome = self.apply(effects);
                if !locked && outcome != Outcome::Destroy {
                    // Countdown redraws were skipped while locked.
                    self.refresh_icon();
                }
                outcome.or_handled(0)
            }
            WM_SETTINGCHANGE | WM_DISPLAYCHANGE | WM_ENVIRONMENT_CHANGED => {
                // Windows broadcasts WM_SETTINGCHANGE for every kind of setting, often several
                // times per change; only act on the ones that affect Awake.
                let change = window_proc::environment_change(message, wparam, lparam);
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
                log::info!("{}", log_lines::console_control_event(wparam));
                self.request_exit(ExitReason::Requested)
            }
            // Closing the window (taskkill, the installer) is an exit, never a silent headless run.
            WM_CLOSE => {
                log::info!("Asked to close.");
                self.request_exit(ExitReason::Requested)
            }
            WM_QUERYENDSESSION => Outcome::Handled(1),
            WM_ENDSESSION => {
                if wparam == 0 {
                    return Outcome::Handled(0);
                }
                if lparam as u32 & ENDSESSION_CLOSEAPP != 0 {
                    // The Restart Manager, for an installer or servicing: the session goes on,
                    // and Windows expects Awake to close itself.
                    log::info!("{}", log_lines::CLOSING_FOR_UPDATE);
                } else {
                    log::info!("Windows is signing out or shutting down.");
                }
                // Windows may end the process as soon as this returns, so clean up right here.
                release_request();
                self.tray.remove();
                logging::flush(Duration::from_millis(500));
                self.request_exit(ExitReason::Requested)
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
            m if m == window_proc::taskbar_created() && m != 0 => {
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

    fn menu_model(&self) -> (Vec<tray_model::MenuItem>, Vec<Preset>) {
        let items = tray_model::menu(&self.engine, self.strings, self.bound.is_some());
        (items, self.engine.presets().to_vec())
    }

    fn on_menu_command(&mut self, id: u16, presets: &[Preset]) -> Outcome {
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

    fn apply(&mut self, effects: Vec<Effect>) -> Outcome {
        let mut outcome = Outcome::Handled(0);
        for effect in effects {
            let result = match effect {
                Effect::SetExecutionState(flags) => {
                    if power::set_execution_state(flags) {
                        log::info!("{}", log_lines::execution_state_set(flags));
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
                    log::info!("{}", log_lines::entered(mode));
                    self.telemetry.mode_entered(mode);
                    Outcome::Handled(0)
                }
                Effect::Exit(reason) => self.request_exit(reason),
            };
            if result == Outcome::Destroy {
                outcome = Outcome::Destroy;
            }
        }
        outcome
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
        session_summary(
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

    fn request_exit(&mut self, reason: ExitReason) -> Outcome {
        crate::hooks::hang_on_exit();
        self.exit.get_or_insert(reason);
        Outcome::Destroy
    }

    fn finish(mut self) -> i32 {
        if let Some(waiter) = self.waiter.take() {
            waiter.stop();
        }
        // The request ends with this thread anyway; releasing it explicitly makes that visible
        // in the log and to anyone inspecting `powercfg /requests` during shutdown. After a
        // Restart Manager close WM_ENDSESSION has already released it, so the line appears twice.
        release_request();
        self.save_before_exit();
        let reason = self.exit.unwrap_or(ExitReason::Requested);
        let farewell = text::farewell(self.strings, self.style, reason);
        match self.live.take() {
            Some(live) => live.finish(format!("{farewell}\n"), Duration::from_secs(1)),
            // A pipe never had a live status line, so there is nothing to clear first.
            None if self.redirected && self.engine.source() == Source::CommandLine => {
                console::write_stdout(farewell.trim_start_matches(['\r', ' ']));
            }
            None => {}
        }
        let code = reason.exit_code();
        // `run` flushes this line before letting a console close end the process.
        log::info!("{}", log_lines::exiting(code));
        code
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

/// What the terminal shows for a command-line session, from the start and from the hand-off.
fn session_summary(
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

/// Whether a timer's effects are only the countdown redraw, which can wait until unlock. Anything
/// else (a session ending at its deadline, say) changes the icon for good and must go through.
fn suppress_while_locked(effects: &[Effect], activity: Activity) -> bool {
    matches!(activity, Activity::Timed { .. }) && effects == [Effect::RefreshIcon]
}

#[cfg(test)]
mod tests {
    use super::*;

    // Resume and lock handlers answer the message themselves, but an exit their effects asked
    // for (a refused power request in a command-line session) must still close the window.
    #[test]
    fn an_exit_asked_for_while_handling_a_notification_is_kept() {
        assert_eq!(Outcome::Destroy.or_handled(1), Outcome::Destroy);
        assert_eq!(Outcome::Handled(0).or_handled(1), Outcome::Handled(1));
        assert_eq!(Outcome::Default.or_handled(0), Outcome::Handled(0));
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
