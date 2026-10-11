//! What the notification-area icon shows: tooltip text and the context menu layout.

use crate::duration::Countdown;
use crate::engine::{Activity, Engine, Source, TrayCommand};
use crate::settings::{Preset, round_up_to_minute};
use crate::strings::{StringId, Strings};
use crate::time::{DateTimeOffset, Timestamp};

/// The name at the start of every tooltip.
pub(crate) const APP_NAME: &str = "PowerToys Awake";

/// Menu command identifiers, numbered from `WM_USER`. A test in the `awake` binary pins them.
pub mod ids {
    /// "Keep screen on".
    pub const DISPLAY: u16 = 0x402;
    /// "Off (keep using the selected power plan)".
    pub const OFF: u16 = 0x403;
    /// "Keep awake indefinitely".
    pub const INDEFINITE: u16 = 0x404;
    /// "Keep awake until expiration date and time"; shown disabled, as a mode indicator.
    pub const EXPIRABLE: u16 = 0x405;
    /// "Exit".
    pub const EXIT: u16 = 0x464;
    /// The first interval preset; the others follow in order.
    pub const FIRST_PRESET: u16 = 0x465;
}

/// One entry of the context menu.
#[derive(Clone, Debug, PartialEq, Eq)]
pub enum MenuItem {
    /// A clickable entry.
    Command {
        /// Sent back in `WM_COMMAND`; see [`command_for`].
        id: u16,
        /// The text shown.
        label: String,
        /// The mark next to it.
        check: Check,
        /// Disabled entries are shown greyed out.
        enabled: bool,
    },
    /// An entry that opens more entries.
    Submenu {
        /// The text shown.
        label: String,
        /// The mark next to it.
        check: Check,
        /// The entries inside.
        items: Vec<MenuItem>,
    },
    /// A dividing line.
    Separator,
}

/// The mark drawn next to a menu entry.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Check {
    /// No mark.
    None,
    /// A checkmark, for on/off settings.
    Checked,
    /// A radio bullet, for one choice out of several.
    Selected,
}

/// Builds the context menu. PowerToys sessions get the full mode menu; command-line sessions
/// only get "Exit", and only when no process binding decides their lifetime.
pub fn menu(engine: &Engine, strings: Strings, bound_to_process: bool) -> Vec<MenuItem> {
    let mut items = Vec::new();
    let activity = engine.activity();
    let radio = |selected: bool| if selected { Check::Selected } else { Check::None };

    if engine.source() == Source::PowerToys {
        let active_seconds = match activity {
            Activity::Timed { seconds, .. } => Some(seconds),
            _ => None,
        };
        let presets = engine
            .presets()
            .iter()
            .enumerate()
            .map(|(index, preset)| MenuItem::Command {
                id: ids::FIRST_PRESET.saturating_add(index as u16),
                label: preset.label.clone(),
                check: radio(active_seconds == Some(round_up_to_minute(preset.seconds))),
                enabled: true,
            })
            .collect();
        items.push(MenuItem::Submenu {
            label: strings.get(StringId::KeepOnInterval).into(),
            check: radio(active_seconds.is_some()),
            items: presets,
        });
        items.push(MenuItem::Command {
            id: ids::EXPIRABLE,
            label: strings.get(StringId::KeepUntilExpiration).into(),
            check: radio(matches!(activity, Activity::Expirable { .. })),
            // A date cannot be picked from a menu; this entry only shows the mode.
            enabled: false,
        });
        items.push(MenuItem::Command {
            id: ids::INDEFINITE,
            label: strings.get(StringId::KeepIndefinitely).into(),
            check: radio(activity == Activity::Indefinite),
            enabled: true,
        });
        items.push(MenuItem::Command {
            id: ids::OFF,
            label: strings.get(StringId::Off).into(),
            check: radio(activity == Activity::Passive),
            enabled: true,
        });
        items.push(MenuItem::Separator);
        items.push(MenuItem::Command {
            id: ids::DISPLAY,
            label: strings.get(StringId::KeepScreenOn).into(),
            check: if engine.keep_display_on() {
                Check::Checked
            } else {
                Check::None
            },
            enabled: activity != Activity::Passive,
        });
    }

    if !bound_to_process {
        if !items.is_empty() {
            items.push(MenuItem::Separator);
        }
        items.push(MenuItem::Command {
            id: ids::EXIT,
            label: strings.get(StringId::Exit).into(),
            check: Check::None,
            enabled: true,
        });
    }
    items
}

/// Maps a menu command back to what it means, using the presets the menu was built from.
pub fn command_for(id: u16, presets: &[Preset]) -> Option<TrayCommand> {
    match id {
        ids::DISPLAY => Some(TrayCommand::ToggleDisplay),
        ids::OFF => Some(TrayCommand::Off),
        ids::INDEFINITE => Some(TrayCommand::Indefinite),
        ids::EXIT => Some(TrayCommand::Exit),
        _ => {
            let index = usize::from(id.checked_sub(ids::FIRST_PRESET)?);
            presets.get(index).map(|preset| TrayCommand::Interval {
                seconds: preset.seconds,
            })
        }
    }
}

/// The built-in presets, used when the settings file defines none.
pub fn default_presets(strings: Strings) -> Vec<Preset> {
    vec![
        Preset {
            label: strings.format(StringId::Minutes, "30"),
            seconds: 30 * 60,
        },
        Preset {
            label: strings.format(StringId::Hour, "1"),
            seconds: 60 * 60,
        },
        Preset {
            label: strings.format(StringId::Hours, "2"),
            seconds: 2 * 60 * 60,
        },
    ]
}

/// Tooltip text, one fact per line.
pub fn tooltip(
    engine: &Engine,
    strings: Strings,
    now: Timestamp,
    bound_pid: Option<u32>,
    format_expiry: &dyn Fn(&DateTimeOffset) -> String,
) -> String {
    let screen = format!(
        "{}: {}",
        strings.get(StringId::TrayDisplay),
        strings.get(if engine.keep_display_on() {
            StringId::ScreenOn
        } else {
            StringId::ScreenOff
        })
    );
    match engine.activity() {
        Activity::Passive => format!("{APP_NAME}\n{}", strings.get(StringId::ScreenOff)),
        Activity::Indefinite => {
            let mut text = format!(
                "{APP_NAME}\n{}\n{screen}",
                strings.get(StringId::TrayTextIndefinite)
            );
            if let Some(pid) = bound_pid {
                text.push_str(&format!("\nPID: {pid}"));
            }
            text
        }
        Activity::Timed { deadline, .. } => format!(
            "{APP_NAME}\n{} {}\n{screen}",
            Countdown(now.secs_until(deadline)),
            strings.get(StringId::TrayRemaining)
        ),
        Activity::Expirable { until } => {
            format!(
                "{APP_NAME}\n{} {}\n{screen}",
                strings.get(StringId::TrayUntil),
                format_expiry(&until)
            )
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::mode::Mode;
    use crate::settings::{AwakeSettings, ModeSetting};

    fn engine(source: Source) -> Engine {
        Engine::new(source, default_presets(Strings::ENGLISH), false)
    }

    fn now() -> DateTimeOffset {
        DateTimeOffset::new(Timestamp::from_ticks(0), 0)
    }

    fn labels(items: &[MenuItem]) -> Vec<String> {
        items
            .iter()
            .map(|item| match item {
                MenuItem::Command { label, .. } | MenuItem::Submenu { label, .. } => label.clone(),
                MenuItem::Separator => "-".into(),
            })
            .collect()
    }

    #[test]
    fn powertoys_menu_layout() {
        let menu = menu(&engine(Source::PowerToys), Strings::ENGLISH, true);
        assert_eq!(
            labels(&menu),
            [
                "Keep awake on interval",
                "Keep awake until expiration date and time",
                "Keep awake indefinitely",
                "Off (keep using the selected power plan)",
                "-",
                "Keep screen on"
            ]
        );
        let MenuItem::Submenu { items, .. } = &menu[0] else {
            panic!()
        };
        assert_eq!(labels(items), ["30 minutes", "1 hour", "2 hours"]);
        assert!(matches!(
            menu[3],
            MenuItem::Command {
                check: Check::Selected,
                ..
            }
        ));
        assert!(
            matches!(menu[5], MenuItem::Command { enabled: false, .. }),
            "screen toggle is off while passive"
        );
    }

    #[test]
    fn exit_only_when_not_bound() {
        let unbound = menu(&engine(Source::PowerToys), Strings::ENGLISH, false);
        assert_eq!(labels(&unbound)[6..], ["-", "Exit"]);
        assert_eq!(
            labels(&menu(&engine(Source::CommandLine), Strings::ENGLISH, false)),
            ["Exit"]
        );
        assert!(menu(&engine(Source::CommandLine), Strings::ENGLISH, true).is_empty());
    }

    #[test]
    fn active_preset_is_selected() {
        let mut engine = engine(Source::PowerToys);
        engine.tray(TrayCommand::Interval { seconds: 3600 }, now());
        let menu = menu(&engine, Strings::ENGLISH, true);
        let MenuItem::Submenu { check, items, .. } = &menu[0] else {
            panic!()
        };
        assert_eq!(*check, Check::Selected);
        let checks: Vec<_> = items
            .iter()
            .map(|i| {
                matches!(
                    i,
                    MenuItem::Command {
                        check: Check::Selected,
                        ..
                    }
                )
            })
            .collect();
        assert_eq!(checks, [false, true, false]);
    }

    #[test]
    fn commands_map_back_through_the_menu_presets() {
        let presets = default_presets(Strings::ENGLISH);
        assert_eq!(
            command_for(ids::FIRST_PRESET + 2, &presets),
            Some(TrayCommand::Interval { seconds: 7200 })
        );
        assert_eq!(command_for(ids::FIRST_PRESET + 3, &presets), None);
        assert_eq!(command_for(ids::EXPIRABLE, &presets), None);
        assert_eq!(command_for(ids::EXIT, &presets), Some(TrayCommand::Exit));
        assert_eq!(command_for(0x401, &presets), None);
    }

    #[test]
    fn tooltips_per_mode() {
        let strings = Strings::ENGLISH;
        let expiry = |_: &DateTimeOffset| "Oct 10, 5:30 PM".to_string();
        let mut engine = engine(Source::PowerToys);
        assert_eq!(
            tooltip(&engine, strings, now().utc, None, &expiry),
            "PowerToys Awake\nOff"
        );

        engine.apply_settings(
            &AwakeSettings {
                mode: ModeSetting::Known(Mode::Indefinite),
                ..Default::default()
            },
            now(),
        );
        assert_eq!(
            tooltip(&engine, strings, now().utc, Some(42), &expiry),
            "PowerToys Awake\nIndefinite\nScreen: Off\nPID: 42"
        );

        engine.tray(TrayCommand::Interval { seconds: 5400 }, now());
        engine.tray(TrayCommand::ToggleDisplay, now());
        let later = now().utc.saturating_add_secs(2);
        assert_eq!(
            tooltip(&engine, strings, later, None, &expiry),
            "PowerToys Awake\n1:29:58 remaining\nScreen: On"
        );

        let until = now().saturating_add_secs(3600);
        let file = AwakeSettings {
            mode: ModeSetting::Known(Mode::Expirable),
            expiration: Some(until),
            keep_display_on: true,
            ..Default::default()
        };
        engine.apply_settings(&file, now());
        assert_eq!(
            tooltip(&engine, strings, now().utc, None, &expiry),
            "PowerToys Awake\nUntil Oct 10, 5:30 PM\nScreen: On"
        );
    }
}
