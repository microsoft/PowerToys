//! The keep-awake state machine.
//!
//! Inputs (settings, tray choices, ticks, lock and power events) go in; [`Effect`]s come out for
//! the Windows layer to carry out. The engine diffs every input against the live state, so a
//! settings write that changes nothing restarts nothing, and its own writes echoing back through
//! the file watcher are no-ops.

use crate::execution_state::{self, RELEASED};
use crate::mode::Mode;
use crate::settings::{AwakeSettings, ModeSetting, Preset, SettingsChange, round_up_to_minute};
use crate::time::{DateTimeOffset, Timestamp};

/// An expiration that is already in the past when read from the settings file is moved this far
/// ahead instead of being dropped, as Awake has always done.
pub const PAST_EXPIRATION_GRACE_SECS: u64 = 5 * 60;

/// Where the session's instructions come from.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Source {
    /// `settings.json`, written by the Settings app, Command Palette, DSC and Awake's own menu.
    PowerToys,
    /// The command line; nothing is persisted and the process exits when the session ends.
    CommandLine,
}

/// The running session.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Activity {
    /// Nothing is held.
    Passive,
    /// Held until something changes it.
    Indefinite,
    /// Held for a fixed time, with a countdown in the tooltip.
    Timed {
        /// The length chosen, kept so re-reading the same setting does not restart the countdown.
        seconds: u64,
        /// When the session ends.
        deadline: Timestamp,
    },
    /// Held until a point in time.
    Expirable {
        /// When the session ends, with the offset it was given in.
        until: DateTimeOffset,
    },
}

impl Activity {
    /// The settings-file mode this activity corresponds to.
    pub fn mode(&self) -> Mode {
        match self {
            Activity::Passive => Mode::Passive,
            Activity::Indefinite => Mode::Indefinite,
            Activity::Timed { .. } => Mode::Timed,
            Activity::Expirable { .. } => Mode::Expirable,
        }
    }

    /// When the session ends on its own, if it does.
    pub fn deadline(&self) -> Option<Timestamp> {
        match self {
            Activity::Timed { deadline, .. } => Some(*deadline),
            Activity::Expirable { until } => Some(until.utc),
            Activity::Passive | Activity::Indefinite => None,
        }
    }
}

/// A choice from the tray menu.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum TrayCommand {
    /// Stop keeping awake.
    Off,
    /// Keep awake until changed.
    Indefinite,
    /// Keep awake for a preset's duration.
    Interval {
        /// The preset's length; rounded up to whole minutes when applied.
        seconds: u32,
    },
    /// Flip "Keep screen on".
    ToggleDisplay,
    /// Quit Awake.
    Exit,
}

/// Why the process is asked to exit.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum ExitReason {
    /// The user chose Exit.
    Requested,
    /// A command-line session reached its end.
    TimeUp,
    /// Windows refused the keep-awake request of a command-line session.
    PowerRequestFailed,
}

/// Something the Windows layer must do, in the order given.
#[derive(Clone, Debug, PartialEq, Eq)]
pub enum Effect {
    /// Call `SetThreadExecutionState` with these flags.
    SetExecutionState(u32),
    /// Arm the one-shot timer that ends the session, or cancel it with `None`.
    ScheduleDeadline(Option<Timestamp>),
    /// Start (`true`) or stop the once-a-second tooltip countdown.
    Countdown(bool),
    /// The tray icon and tooltip are out of date.
    RefreshIcon,
    /// Write these values to `settings.json`.
    Persist(Vec<SettingsChange>),
    /// The session entered a mode; reported to telemetry.
    ModeEntered(Mode),
    /// End the process.
    Exit(ExitReason),
}

/// The keep-awake state machine; one per process, driven from the window thread.
#[derive(Debug)]
pub struct Engine {
    source: Source,
    activity: Activity,
    keep_display_on: bool,
    screen_locked: bool,
    /// What this thread last asked Windows for; starts as "nothing held".
    applied_flags: u32,
    presets: Vec<Preset>,
    default_presets: Vec<Preset>,
}

impl Engine {
    /// A passive engine that holds nothing yet. `default_presets` fill the menu whenever the
    /// settings file has no custom ones.
    pub fn new(source: Source, default_presets: Vec<Preset>, screen_locked: bool) -> Self {
        Engine {
            source,
            activity: Activity::Passive,
            keep_display_on: false,
            screen_locked,
            applied_flags: RELEASED,
            presets: default_presets.clone(),
            default_presets,
        }
    }

    /// Where this session's instructions come from.
    pub fn source(&self) -> Source {
        self.source
    }

    /// The running session.
    pub fn activity(&self) -> Activity {
        self.activity
    }

    /// Whether the user wants the display kept on; while locked it is still released.
    pub fn keep_display_on(&self) -> bool {
        self.keep_display_on
    }

    /// Whether the session is locked, as last reported.
    pub fn screen_locked(&self) -> bool {
        self.screen_locked
    }

    /// The presets the menu shows: the custom ones from the settings file, or the defaults. The
    /// menu is built from these each time it opens.
    pub fn presets(&self) -> &[Preset] {
        &self.presets
    }

    /// Starts a command-line session.
    pub fn start(&mut self, activity: Activity, keep_display_on: bool) -> Vec<Effect> {
        let mut effects = Vec::new();
        self.transition(activity, keep_display_on, &mut effects);
        effects
    }

    /// Reconciles with `settings.json` after it was read.
    pub fn apply_settings(&mut self, settings: &AwakeSettings, now: DateTimeOffset) -> Vec<Effect> {
        let mut effects = Vec::new();
        if self.source != Source::PowerToys {
            return effects;
        }

        let presets = if settings.custom_tray_times.is_empty() {
            &self.default_presets
        } else {
            &settings.custom_tray_times
        };
        if *presets != self.presets {
            self.presets = presets.clone();
        }

        let activity = match settings.mode {
            // Leave the session alone; the caller logs the unexpected value.
            ModeSetting::Unknown(_) => self.activity,
            ModeSetting::Known(Mode::Passive) => Activity::Passive,
            ModeSetting::Known(Mode::Indefinite) => Activity::Indefinite,
            ModeSetting::Known(Mode::Timed) => {
                let seconds = settings.timed_seconds();
                match self.activity {
                    // Same duration: keep counting down instead of starting over.
                    Activity::Timed {
                        seconds: running,
                        deadline,
                    } if running == seconds => Activity::Timed { seconds, deadline },
                    _ => Activity::Timed {
                        seconds,
                        deadline: now.utc.saturating_add_secs(seconds),
                    },
                }
            }
            ModeSetting::Known(Mode::Expirable) => match settings.expiration.filter(|at| at.utc > now.utc) {
                Some(until) => Activity::Expirable { until },
                None => {
                    let until = now.saturating_add_secs(PAST_EXPIRATION_GRACE_SECS);
                    effects.push(Effect::Persist(vec![SettingsChange::Expiration(until)]));
                    Activity::Expirable { until }
                }
            },
        };
        self.transition(activity, settings.keep_display_on, &mut effects);
        effects
    }

    /// Handles a choice from the tray menu.
    pub fn tray(&mut self, command: TrayCommand, now: DateTimeOffset) -> Vec<Effect> {
        let mut effects = Vec::new();
        let changes = match command {
            TrayCommand::Exit => {
                effects.push(Effect::Exit(ExitReason::Requested));
                return effects;
            }
            TrayCommand::Off => {
                self.transition(Activity::Passive, self.keep_display_on, &mut effects);
                vec![SettingsChange::Mode(Mode::Passive)]
            }
            TrayCommand::Indefinite => {
                self.transition(Activity::Indefinite, self.keep_display_on, &mut effects);
                vec![SettingsChange::Mode(Mode::Indefinite)]
            }
            TrayCommand::Interval { seconds } => {
                // The settings file stores hours and minutes, so whole minutes are what will be
                // read back; rounding up here keeps the echo from restarting the countdown.
                let seconds = round_up_to_minute(seconds);
                if seconds == 0 {
                    return effects;
                }
                let deadline = now.utc.saturating_add_secs(seconds);
                self.transition(
                    Activity::Timed { seconds, deadline },
                    self.keep_display_on,
                    &mut effects,
                );
                let hours = u32::try_from(seconds / 3600).unwrap_or(u32::MAX);
                let minutes = (seconds % 3600 / 60) as u32;
                vec![
                    SettingsChange::Mode(Mode::Timed),
                    SettingsChange::Interval { hours, minutes },
                ]
            }
            TrayCommand::ToggleDisplay => {
                if self.activity == Activity::Passive {
                    return effects;
                }
                let on = !self.keep_display_on;
                self.transition(self.activity, on, &mut effects);
                vec![SettingsChange::KeepDisplayOn(on)]
            }
        };
        if self.source == Source::PowerToys {
            effects.push(Effect::Persist(changes));
        }
        effects
    }

    /// Called once a second while counting down.
    pub fn tick(&mut self, now: DateTimeOffset) -> Vec<Effect> {
        match self.activity.deadline() {
            Some(deadline) if now.utc >= deadline => self.end_session(ExitReason::TimeUp),
            Some(_) if matches!(self.activity, Activity::Timed { .. }) => vec![Effect::RefreshIcon],
            _ => Vec::new(),
        }
    }

    /// Called when the deadline timer fires. The timer runs on interrupt time, which clock
    /// corrections do not move, so it can fire while the system clock still reads a little
    /// before the deadline; it is then armed again rather than leaving the session without
    /// an end.
    pub fn deadline_fired(&mut self, now: DateTimeOffset) -> Vec<Effect> {
        match self.activity.deadline() {
            Some(deadline) if now.utc < deadline => {
                let mut effects = vec![Effect::ScheduleDeadline(Some(deadline))];
                effects.extend(self.tick(now));
                effects
            }
            _ => self.tick(now),
        }
    }

    /// The session was locked or unlocked; the display request follows, the system one stays.
    pub fn set_screen_locked(&mut self, locked: bool) -> Vec<Effect> {
        let mut effects = Vec::new();
        if locked != self.screen_locked {
            self.screen_locked = locked;
            self.sync_flags(false, &mut effects);
        }
        effects
    }

    /// Resume from sleep or a power source change: assert the request again.
    pub fn power_changed(&mut self) -> Vec<Effect> {
        let mut effects = Vec::new();
        self.sync_flags(true, &mut effects);
        effects
    }

    /// Windows rejected the request, so nothing is held; stop claiming otherwise.
    pub fn execution_state_failed(&mut self) -> Vec<Effect> {
        self.applied_flags = RELEASED;
        self.end_session(ExitReason::PowerRequestFailed)
    }

    fn end_session(&mut self, reason: ExitReason) -> Vec<Effect> {
        let mut effects = Vec::new();
        self.transition(Activity::Passive, self.keep_display_on, &mut effects);
        match self.source {
            Source::PowerToys => effects.push(Effect::Persist(vec![SettingsChange::Mode(Mode::Passive)])),
            Source::CommandLine => effects.push(Effect::Exit(reason)),
        }
        effects
    }

    fn desired_flags(&self) -> u32 {
        match self.activity {
            Activity::Passive => RELEASED,
            _ => execution_state::keep_awake(self.keep_display_on, self.screen_locked),
        }
    }

    fn sync_flags(&mut self, reassert: bool, effects: &mut Vec<Effect>) {
        let flags = self.desired_flags();
        let changed = flags != self.applied_flags;
        if changed || (reassert && flags != RELEASED) {
            self.applied_flags = flags;
            effects.push(Effect::SetExecutionState(flags));
        }
    }

    fn transition(&mut self, activity: Activity, keep_display_on: bool, effects: &mut Vec<Effect>) {
        let previous = self.activity;
        let display_changed = keep_display_on != self.keep_display_on;
        self.activity = activity;
        self.keep_display_on = keep_display_on;

        let mode_changed = previous.mode() != activity.mode();
        let deadline_changed = previous.deadline() != activity.deadline();

        if mode_changed || (deadline_changed && activity.deadline().is_some()) {
            effects.push(Effect::ModeEntered(activity.mode()));
        }
        if deadline_changed {
            effects.push(Effect::ScheduleDeadline(activity.deadline()));
        }
        let counting = matches!(activity, Activity::Timed { .. });
        if counting != matches!(previous, Activity::Timed { .. }) || (counting && deadline_changed) {
            effects.push(Effect::Countdown(counting));
        }
        self.sync_flags(false, effects);
        if mode_changed || display_changed || deadline_changed {
            effects.push(Effect::RefreshIcon);
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::execution_state::{ES_CONTINUOUS, ES_DISPLAY_REQUIRED, ES_SYSTEM_REQUIRED};

    const SYSTEM: u32 = ES_SYSTEM_REQUIRED | ES_CONTINUOUS;
    const DISPLAY: u32 = ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED | ES_CONTINUOUS;

    fn at(seconds: u64) -> DateTimeOffset {
        DateTimeOffset::new(Timestamp::from_ticks(0).saturating_add_secs(seconds), 0)
    }

    fn defaults() -> Vec<Preset> {
        vec![
            Preset {
                label: "30 minutes".into(),
                seconds: 1800,
            },
            Preset {
                label: "1 hour".into(),
                seconds: 3600,
            },
        ]
    }

    fn powertoys() -> Engine {
        Engine::new(Source::PowerToys, defaults(), false)
    }

    fn settings(mode: Mode) -> AwakeSettings {
        AwakeSettings {
            mode: ModeSetting::Known(mode),
            ..AwakeSettings::default()
        }
    }

    fn timed(hours: u32, minutes: u32) -> AwakeSettings {
        AwakeSettings {
            interval_hours: hours,
            interval_minutes: minutes,
            ..settings(Mode::Timed)
        }
    }

    fn flags(effects: &[Effect]) -> Vec<u32> {
        effects
            .iter()
            .filter_map(|e| match e {
                Effect::SetExecutionState(f) => Some(*f),
                _ => None,
            })
            .collect()
    }

    #[test]
    fn indefinite_holds_the_system_and_passive_releases_it() {
        let mut engine = powertoys();
        let effects = engine.apply_settings(&settings(Mode::Indefinite), at(0));
        assert_eq!(flags(&effects), [SYSTEM]);
        assert!(effects.contains(&Effect::ModeEntered(Mode::Indefinite)));
        assert_eq!(
            flags(&engine.apply_settings(&settings(Mode::Passive), at(1))),
            [ES_CONTINUOUS]
        );
    }

    #[test]
    fn rereading_identical_settings_does_nothing() {
        let mut engine = powertoys();
        engine.apply_settings(&timed(0, 30), at(0));
        assert_eq!(engine.apply_settings(&timed(0, 30), at(100)), []);
        assert_eq!(
            engine.activity().deadline(),
            Some(at(1800).utc),
            "the countdown kept running"
        );
    }

    #[test]
    fn display_toggle_keeps_the_countdown() {
        let mut engine = powertoys();
        engine.apply_settings(&timed(1, 0), at(0));
        let effects = engine.tray(TrayCommand::ToggleDisplay, at(600));
        assert_eq!(flags(&effects), [DISPLAY]);
        assert!(
            !effects
                .iter()
                .any(|e| matches!(e, Effect::ScheduleDeadline(_) | Effect::Countdown(_)))
        );
        assert_eq!(
            effects.last(),
            Some(&Effect::Persist(vec![SettingsChange::KeepDisplayOn(true)]))
        );

        // The echo of that write through the file watcher must be a no-op.
        let echo = AwakeSettings {
            keep_display_on: true,
            ..timed(1, 0)
        };
        assert_eq!(engine.apply_settings(&echo, at(601)), []);
        assert_eq!(engine.activity().deadline(), Some(at(3600).utc));
    }

    #[test]
    fn changing_the_duration_restarts_the_countdown() {
        let mut engine = powertoys();
        engine.apply_settings(&timed(0, 30), at(0));
        let effects = engine.apply_settings(&timed(0, 45), at(60));
        assert!(effects.contains(&Effect::ScheduleDeadline(Some(at(60 + 2700).utc))));
        assert!(effects.contains(&Effect::Countdown(true)));
        assert!(
            flags(&effects).is_empty(),
            "the request is never dropped in between"
        );
    }

    #[test]
    fn tray_interval_rounds_to_minutes_so_the_echo_is_a_no_op() {
        let mut engine = powertoys();
        let effects = engine.tray(TrayCommand::Interval { seconds: 3599 }, at(0));
        assert_eq!(
            effects.last(),
            Some(&Effect::Persist(vec![
                SettingsChange::Mode(Mode::Timed),
                SettingsChange::Interval { hours: 1, minutes: 0 }
            ]))
        );
        assert_eq!(engine.apply_settings(&timed(1, 0), at(1)), []);
    }

    #[test]
    fn choosing_the_same_preset_again_restarts_it() {
        let mut engine = powertoys();
        engine.tray(TrayCommand::Interval { seconds: 1800 }, at(0));
        let effects = engine.tray(TrayCommand::Interval { seconds: 1800 }, at(300));
        assert!(effects.contains(&Effect::ScheduleDeadline(Some(at(2100).utc))));
        assert!(effects.contains(&Effect::ModeEntered(Mode::Timed)));
    }

    #[test]
    fn timed_session_ends_in_passive_and_is_persisted() {
        let mut engine = powertoys();
        engine.apply_settings(&timed(0, 1), at(0));
        assert_eq!(engine.tick(at(30)), [Effect::RefreshIcon]);
        let effects = engine.tick(at(60));
        assert_eq!(flags(&effects), [ES_CONTINUOUS]);
        assert!(effects.contains(&Effect::ScheduleDeadline(None)));
        assert!(effects.contains(&Effect::Countdown(false)));
        assert_eq!(
            effects.last(),
            Some(&Effect::Persist(vec![SettingsChange::Mode(Mode::Passive)]))
        );
        assert_eq!(engine.tick(at(61)), [], "it completes exactly once");
    }

    #[test]
    fn expirable_session_ends_when_the_time_comes() {
        let mut engine = powertoys();
        let until = at(100);
        engine.apply_settings(
            &AwakeSettings {
                expiration: Some(until),
                ..settings(Mode::Expirable)
            },
            at(0),
        );
        assert_eq!(engine.tick(at(99)), [], "expirable sessions show no countdown");
        let effects = engine.tick(at(100));
        assert_eq!(flags(&effects), [ES_CONTINUOUS]);
        assert!(effects.contains(&Effect::ScheduleDeadline(None)));
        assert!(
            !effects.iter().any(|e| matches!(e, Effect::Countdown(_))),
            "no countdown was running"
        );
        assert_eq!(
            effects.last(),
            Some(&Effect::Persist(vec![SettingsChange::Mode(Mode::Passive)]))
        );
        assert_eq!(engine.tick(at(101)), [], "it completes exactly once");
    }

    #[test]
    fn command_line_expirable_session_exits_when_the_time_comes() {
        let mut engine = Engine::new(Source::CommandLine, defaults(), false);
        engine.start(Activity::Expirable { until: at(100) }, false);
        assert_eq!(engine.tick(at(99)), []);
        let effects = engine.tick(at(100));
        assert_eq!(effects.last(), Some(&Effect::Exit(ExitReason::TimeUp)));
        assert!(!effects.iter().any(|e| matches!(e, Effect::Persist(_))));
    }

    // The Settings app can save 0 hours and 0 minutes. That session is due the moment it starts
    // and must end on the first tick, once, rather than holding the system awake.
    #[test]
    fn zero_length_timed_settings_end_on_the_first_tick() {
        let mut engine = powertoys();
        let effects = engine.apply_settings(&timed(0, 0), at(0));
        assert!(effects.contains(&Effect::ScheduleDeadline(Some(at(0).utc))));
        let effects = engine.tick(at(0));
        assert_eq!(engine.activity(), Activity::Passive);
        assert_eq!(flags(&effects), [ES_CONTINUOUS]);
        assert_eq!(
            effects
                .iter()
                .filter(|e| matches!(e, Effect::Persist(_)))
                .collect::<Vec<_>>(),
            [&Effect::Persist(vec![SettingsChange::Mode(Mode::Passive)])]
        );
        assert_eq!(engine.tick(at(1)), []);
        assert_eq!(
            engine.apply_settings(&settings(Mode::Passive), at(1)),
            [],
            "the echo of the write is a no-op"
        );
    }

    #[test]
    fn early_deadline_timer_is_armed_again() {
        let mut engine = powertoys();
        let deadline = at(3600);
        let settings = AwakeSettings {
            expiration: Some(deadline),
            ..settings(Mode::Expirable)
        };
        engine.apply_settings(&settings, at(0));
        assert_eq!(
            engine.deadline_fired(at(3599)),
            [Effect::ScheduleDeadline(Some(deadline.utc))]
        );
        assert_eq!(engine.activity().mode(), Mode::Expirable);
        let effects = engine.deadline_fired(at(3600));
        assert!(effects.contains(&Effect::ScheduleDeadline(None)));
        assert_eq!(
            effects.last(),
            Some(&Effect::Persist(vec![SettingsChange::Mode(Mode::Passive)]))
        );
        assert_eq!(engine.deadline_fired(at(3601)), [], "a stale timer does nothing");
    }

    #[test]
    fn command_line_session_exits_when_time_is_up() {
        let mut engine = Engine::new(Source::CommandLine, defaults(), false);
        engine.start(
            Activity::Timed {
                seconds: 5,
                deadline: at(5).utc,
            },
            false,
        );
        let effects = engine.tick(at(5));
        assert_eq!(effects.last(), Some(&Effect::Exit(ExitReason::TimeUp)));
        assert!(!effects.iter().any(|e| matches!(e, Effect::Persist(_))));
    }

    #[test]
    fn past_expiration_from_file_is_pushed_five_minutes_ahead() {
        let mut engine = powertoys();
        let stale = AwakeSettings {
            expiration: Some(at(10)),
            ..settings(Mode::Expirable)
        };
        let effects = engine.apply_settings(&stale, at(100));
        let until = at(100 + PAST_EXPIRATION_GRACE_SECS);
        assert!(effects.contains(&Effect::Persist(vec![SettingsChange::Expiration(until)])));
        assert_eq!(engine.activity(), Activity::Expirable { until });

        let echo = AwakeSettings {
            expiration: Some(until),
            ..settings(Mode::Expirable)
        };
        assert_eq!(engine.apply_settings(&echo, at(101)), []);
    }

    #[test]
    fn locking_drops_and_unlocking_restores_the_display() {
        let mut engine = powertoys();
        engine.apply_settings(
            &AwakeSettings {
                keep_display_on: true,
                ..settings(Mode::Indefinite)
            },
            at(0),
        );
        assert_eq!(flags(&engine.set_screen_locked(true)), [SYSTEM]);
        assert_eq!(
            engine.set_screen_locked(true),
            [],
            "repeated lock events change nothing"
        );
        assert_eq!(flags(&engine.set_screen_locked(false)), [DISPLAY]);
    }

    #[test]
    fn lock_changes_while_passive_request_nothing() {
        let mut engine = powertoys();
        assert_eq!(engine.set_screen_locked(true), []);
        assert_eq!(engine.power_changed(), []);
    }

    #[test]
    fn resume_reasserts_the_request() {
        let mut engine = powertoys();
        engine.apply_settings(&settings(Mode::Indefinite), at(0));
        assert_eq!(engine.power_changed(), [Effect::SetExecutionState(SYSTEM)]);
    }

    #[test]
    fn unknown_mode_leaves_the_session_alone() {
        let mut engine = powertoys();
        engine.apply_settings(&settings(Mode::Indefinite), at(0));
        let odd = AwakeSettings {
            mode: ModeSetting::Unknown(9),
            ..AwakeSettings::default()
        };
        assert!(flags(&engine.apply_settings(&odd, at(1))).is_empty());
        assert_eq!(engine.activity(), Activity::Indefinite);
    }

    #[test]
    fn failure_to_hold_the_system_becomes_a_real_transition() {
        let mut engine = powertoys();
        engine.apply_settings(&timed(1, 0), at(0));
        let effects = engine.execution_state_failed();
        assert_eq!(engine.activity(), Activity::Passive);
        assert!(effects.contains(&Effect::ScheduleDeadline(None)));
        assert!(effects.contains(&Effect::Persist(vec![SettingsChange::Mode(Mode::Passive)])));

        let mut standalone = Engine::new(Source::CommandLine, defaults(), false);
        standalone.start(Activity::Indefinite, false);
        assert_eq!(
            standalone.execution_state_failed().last(),
            Some(&Effect::Exit(ExitReason::PowerRequestFailed))
        );
    }

    #[test]
    fn display_toggle_is_ignored_while_passive() {
        let mut engine = powertoys();
        assert_eq!(engine.tray(TrayCommand::ToggleDisplay, at(0)), []);
    }

    #[test]
    fn custom_presets_replace_the_defaults() {
        let mut engine = powertoys();
        let custom = vec![Preset {
            label: "5 minutes".into(),
            seconds: 300,
        }];
        let effects = engine.apply_settings(
            &AwakeSettings {
                custom_tray_times: custom.clone(),
                ..Default::default()
            },
            at(0),
        );
        assert_eq!(effects, [], "nothing to do until the menu opens");
        assert_eq!(engine.presets(), custom.as_slice());
        engine.apply_settings(&AwakeSettings::default(), at(1));
        assert_eq!(engine.presets(), defaults().as_slice());
    }

    #[test]
    fn command_line_engine_ignores_settings_and_never_persists() {
        let mut engine = Engine::new(Source::CommandLine, defaults(), false);
        engine.start(Activity::Indefinite, false);
        assert_eq!(engine.apply_settings(&settings(Mode::Passive), at(0)), []);
        let effects = engine.tray(TrayCommand::Off, at(0));
        assert!(!effects.iter().any(|e| matches!(e, Effect::Persist(_))));
    }

    #[test]
    fn exit_from_the_menu() {
        assert_eq!(
            powertoys().tray(TrayCommand::Exit, at(0)),
            [Effect::Exit(ExitReason::Requested)]
        );
    }

    #[test]
    fn thousands_of_mode_switches_leave_no_residue() {
        let mut engine = powertoys();
        for i in 0..5_000u64 {
            let next = match i % 4 {
                0 => settings(Mode::Indefinite),
                1 => timed(0, 1 + (i % 7) as u32),
                2 => AwakeSettings {
                    expiration: Some(at(i + 1_000)),
                    ..settings(Mode::Expirable)
                },
                _ => settings(Mode::Passive),
            };
            engine.apply_settings(&next, at(i));
        }
        assert_eq!(engine.activity(), Activity::Passive);
        assert_eq!(engine.desired_flags(), RELEASED);
        assert_eq!(engine.applied_flags, RELEASED);
        assert_eq!(engine.presets().len(), 2);
    }
}
