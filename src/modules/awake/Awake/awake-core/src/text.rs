//! Text for the terminal: help, errors and the live session display.
//!
//! The look: an accented title, `●` for what is happening, `⎿` for details,
//! and one dim status line that updates in place.

use crate::cli::{CliError, Expected, PROGRAM, UntilError};
use crate::duration::{Countdown, DurationError};
use crate::engine::ExitReason;
use crate::strings::{StringId, Strings};

// From `[workspace.package]`, so the help text and the manifest cannot drift apart.
const AUTHOR: &str = env!("CARGO_PKG_AUTHORS");
const HOMEPAGE: &str = env!("CARGO_PKG_HOMEPAGE");

/// How much the terminal can show.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct Style {
    /// Virtual terminal sequences work (cursor and line control).
    pub vt: bool,
    /// ANSI colors; off with `NO_COLOR` even when `vt` is on.
    pub color: bool,
    /// Glyphs that need a modern terminal font, such as Windows Terminal's.
    pub rich_glyphs: bool,
}

impl Style {
    /// No terminal features at all, as when output is redirected to a file.
    pub const PLAIN: Style = Style {
        vt: false,
        color: false,
        rich_glyphs: false,
    };

    // Not `⏺`: terminals draw it as a two-cell emoji that overlaps the text after it.
    const BULLET: &str = "●";

    fn paint(self, code: &str, text: &str) -> String {
        if self.color && !text.is_empty() {
            format!("\x1b[{code}m{text}\x1b[0m")
        } else {
            text.to_owned()
        }
    }

    // Windows 11's dark-theme accent blue (#4CC2FF).
    fn accent(self, text: &str) -> String {
        self.paint("1;38;2;76;194;255", text)
    }

    fn bold(self, text: &str) -> String {
        self.paint("1", text)
    }

    fn dim(self, text: &str) -> String {
        self.paint("2", text)
    }

    fn red(self, text: &str) -> String {
        self.paint("31", text)
    }

    fn green(self, text: &str) -> String {
        self.paint("32", text)
    }

    fn title_glyph(self) -> &'static str {
        if self.rich_glyphs { "◉" } else { "*" }
    }

    fn detail(self) -> &'static str {
        if self.rich_glyphs { "⎿" } else { "└" }
    }

    /// Moves to the start of the line and clears it, so the status line can be redrawn.
    pub fn clear_line(self) -> &'static str {
        // Without line control, blanks overwrite whatever a longer earlier line left behind.
        if self.vt {
            "\r\x1b[2K"
        } else {
            "\r                                                                        \r"
        }
    }
}

/// The `--version` output.
pub fn version_line(version: &str, build_id: &str) -> String {
    format!("{version} (PowerToys Awake, build {build_id})\n")
}

/// The `--help` output, with the columns aligned for the current language.
pub fn help(strings: Strings, style: Style, version: &str) -> String {
    let s = |id| strings.get(id);
    let duration = format!("<{}>", s(StringId::CliArgDuration));
    let time = format!("<{}>", s(StringId::CliArgTime));
    let commands = [
        (format!("for {duration}"), s(StringId::CliCmdFor)),
        (format!("until {time}"), s(StringId::CliCmdUntil)),
    ];
    let options = [
        ("-d, --display-on".to_owned(), s(StringId::CliOptDisplay)),
        ("    --pid <pid>".to_owned(), s(StringId::CliOptPid)),
        ("    --parent".to_owned(), s(StringId::CliOptParent)),
        ("-h, --help".to_owned(), s(StringId::CliOptHelp)),
        ("-v, --version".to_owned(), s(StringId::CliOptVersion)),
    ];
    let examples = [
        (PROGRAM.to_owned(), s(StringId::CliCmdDefault)),
        (format!("{PROGRAM} for 1h30m --display-on"), ""),
        (format!("{PROGRAM} until 18:00 --parent"), ""),
    ];
    // Rows without a description do not need the column, so they do not widen it.
    let width = commands
        .iter()
        .chain(&options)
        .chain(&examples)
        .filter(|(_, right)| !right.is_empty())
        .map(|(left, _)| display_width(left))
        .max()
        .unwrap_or(0);

    let mut out = format!(
        "{} {}\n  {}\n\n",
        style.accent(style.title_glyph()),
        style.bold(&format!("PowerToys Awake {version}")),
        s(StringId::CliDescription)
    );
    out.push_str(&format!(
        "{}: {PROGRAM} [options] [command]\n",
        style.bold(s(StringId::CliUsage))
    ));
    for (heading, rows) in [
        (s(StringId::CliCommands), &commands[..]),
        (s(StringId::CliOptions), &options[..]),
        (s(StringId::CliExamples), &examples[..]),
    ] {
        out.push_str(&format!("\n{}:\n", style.bold(heading)));
        for (left, right) in rows {
            let padding = " ".repeat(width.saturating_sub(display_width(left)) + 2);
            out.push_str(format!("  {}{padding}{}", style.accent(left), style.dim(right)).trim_end());
            out.push('\n');
        }
    }
    out.push_str(&format!(
        "\n{}\n",
        style.dim(&format!(
            "{} · {HOMEPAGE}",
            strings.format(StringId::CliAuthor, AUTHOR)
        ))
    ));
    out
}

/// An error report: the first line of `message` as the headline, any further lines as details,
/// then a pointer to `--help`.
pub fn error(strings: Strings, style: Style, message: &str) -> String {
    let hint = strings.format(StringId::CliHintHelp, PROGRAM);
    let mut lines = message.lines();
    let mut out = format!(
        "{} {}: {}\n",
        style.red(Style::BULLET),
        style.bold(strings.get(StringId::CliError)),
        lines.next().unwrap_or_default()
    );
    for extra in lines {
        out.push_str(&format!("  {} {}\n", style.dim(style.detail()), style.dim(extra)));
    }
    out.push_str(&format!("  {} {}\n", style.dim(style.detail()), style.dim(&hint)));
    out
}

/// The localized message for a rejected command line, to pass to [`error`].
pub fn cli_error_message(strings: Strings, error: &CliError) -> String {
    match error {
        CliError::Usage(text) => text.clone(),
        CliError::Duration {
            input,
            error: DurationError::TooLong,
        } => {
            format!(
                "{} (max {})",
                strings.format(StringId::CliErrDuration, input),
                crate::duration::Human(crate::duration::MAX_SECONDS)
            )
        }
        CliError::Duration { input, .. } => strings.format(StringId::CliErrDuration, input),
        CliError::Time { input } => strings.format(StringId::CliErrTime, input),
        // A number too large for any process reads like one no process has.
        CliError::Pid { input } if is_integer(input) => {
            strings.format(StringId::CliErrPidNotFound, input.trim())
        }
        CliError::Pid { input } => strings.format(StringId::CliErrPidInvalid, input),
        CliError::Seconds { input } => strings.format(StringId::CliErrSeconds, input),
        CliError::MissingValue { name, expected } => strings.format(
            match expected {
                Expected::Duration => StringId::CliErrNeedsDuration,
                Expected::Time => StringId::CliErrNeedsTime,
                Expected::ProcessId => StringId::CliErrNeedsPid,
                Expected::Seconds => StringId::CliErrNeedsSeconds,
            },
            name,
        ),
    }
}

/// The localized message for an `until` time that [`crate::cli::resolve_until`] turned down.
pub fn until_error_message(strings: Strings, error: &UntilError) -> String {
    match error {
        UntilError::Unreadable(input) => strings.format(StringId::CliErrTime, input),
        UntilError::Past => strings.get(StringId::CliErrPast).to_owned(),
    }
}

fn is_integer(text: &str) -> bool {
    let text = text.trim();
    let digits = text.strip_prefix(['-', '+']).unwrap_or(text);
    !digits.is_empty() && digits.bytes().all(|b| b.is_ascii_digit())
}

/// What the terminal shows for a running session.
#[derive(Clone, Debug, PartialEq, Eq)]
pub struct SessionSummary {
    /// The first line, such as "Keeping awake for 1h 30m".
    pub headline: String,
    /// Whether the display is kept on too.
    pub display_on: bool,
    /// The process the session ends with, as the user should see it.
    pub bound_to: Option<String>,
}

/// The block printed once when a command-line session starts.
pub fn session_header(strings: Strings, style: Style, version: &str, summary: &SessionSummary) -> String {
    let display = strings.get(if summary.display_on {
        StringId::CliDisplayOn
    } else {
        StringId::CliDisplayOff
    });
    let mut out = format!(
        "{} {} {}\n\n{} {}\n  {} {display}\n",
        style.accent(style.title_glyph()),
        style.bold("PowerToys Awake"),
        style.dim(version),
        style.accent(Style::BULLET),
        summary.headline,
        style.dim(style.detail()),
    );
    if let Some(target) = &summary.bound_to {
        out.push_str(&format!(
            "  {} {}\n",
            style.dim(style.detail()),
            strings.format(StringId::CliBound, target)
        ));
    }
    out.push('\n');
    out
}

/// The line that updates in place; `remaining` is `None` for open-ended sessions.
pub fn status_line(strings: Strings, style: Style, remaining: Option<u64>) -> String {
    let hint = strings.get(StringId::CliHintStop);
    let text = match remaining {
        Some(seconds) => {
            format!(
                "{} · {hint}",
                strings.format(StringId::CliRemaining, &Countdown(seconds).to_string())
            )
        }
        None => hint.to_owned(),
    };
    format!("{}  {}", style.clear_line(), style.dim(&text))
}

/// The last line, replacing the status line, saying why the session ended.
pub fn farewell(strings: Strings, style: Style, reason: ExitReason) -> String {
    let message = match reason {
        ExitReason::Requested => StringId::CliStopped,
        ExitReason::TimeUp => StringId::CliFinished,
        ExitReason::TargetExited => StringId::CliTargetExited,
        ExitReason::PowerRequestFailed => {
            return format!(
                "{}{}",
                style.clear_line(),
                error(strings, style, strings.get(StringId::CliErrPower))
            );
        }
    };
    format!(
        "{}{} {}\n",
        style.clear_line(),
        style.green(Style::BULLET),
        strings.get(message)
    )
}

/// Terminal columns a string occupies; East Asian wide characters take two.
pub(crate) fn display_width(text: &str) -> usize {
    text.chars()
        .map(|c| {
            let wide = matches!(c as u32,
                0x1100..=0x115F | 0x2E80..=0xA4CF | 0xAC00..=0xD7A3 | 0xF900..=0xFAFF
                | 0xFE30..=0xFE4F | 0xFF00..=0xFF60 | 0xFFE0..=0xFFE6 | 0x20000..=0x3FFFD);
            if wide { 2 } else { 1 }
        })
        .sum()
}

#[cfg(test)]
mod tests {
    use super::*;

    const COLOR: Style = Style {
        vt: true,
        color: true,
        rich_glyphs: true,
    };

    #[test]
    fn help_lists_commands_options_and_attribution() {
        let text = help(Strings::ENGLISH, Style::PLAIN, "0.0.1");
        for needle in [
            "PowerToys Awake 0.0.1",
            "Usage: PowerToys.Awake [options] [command]",
            "for <duration>",
            "until <time>",
            "-d, --display-on",
            "--pid <pid>",
            "--parent",
            "Created by Den Delimarsky",
            "https://awake.den.dev",
        ] {
            assert!(text.contains(needle), "missing {needle:?} in\n{text}");
        }
        assert!(!text.contains("--use-pt-config"), "legacy options stay hidden");
        assert!(!text.contains('\x1b'), "plain style has no escape codes");
    }

    #[test]
    fn help_columns_line_up() {
        let text = help(Strings::ENGLISH, Style::PLAIN, "1");
        let column = |needle: &str| {
            let line = text.lines().find(|l| l.contains(needle)).unwrap();
            line.find(if needle == "for <duration>" {
                "Keep awake for"
            } else {
                "Keep the display"
            })
            .unwrap()
        };
        assert_eq!(column("for <duration>"), column("--display-on"));
    }

    #[test]
    fn colored_output_resets_every_span() {
        let text = help(Strings::ENGLISH, COLOR, "1");
        assert_eq!(
            text.matches("\x1b[0m").count(),
            text.matches('\x1b').count() - text.matches("\x1b[0m").count()
        );
    }

    #[test]
    fn errors_carry_a_hint() {
        let text = error(
            Strings::ENGLISH,
            Style::PLAIN,
            "unexpected argument '--x' found\ntip: try '--pid'",
        );
        assert_eq!(
            text,
            "● Error: unexpected argument '--x' found\n  └ tip: try '--pid'\n  └ Run PowerToys.Awake --help to see what Awake can do.\n"
        );
    }

    #[test]
    fn cli_errors_are_localized_sentences() {
        let message = cli_error_message(
            Strings::ENGLISH,
            &CliError::Duration {
                input: "90".into(),
                error: DurationError::MissingUnit,
            },
        );
        assert_eq!(message, "\"90\" isn't a duration. Try 45m, 2h or 1h30m.");
        let seconds = cli_error_message(Strings::ENGLISH, &CliError::Seconds { input: "abc".into() });
        assert_eq!(
            seconds,
            "\"abc\" isn't a number of seconds. Try 3600 for one hour."
        );
    }

    #[test]
    fn until_errors_are_localized_sentences() {
        let unreadable = until_error_message(Strings::ENGLISH, &UntilError::Unreadable("soonish".into()));
        assert_eq!(
            unreadable,
            "\"soonish\" isn't a time Awake understands. Try 17:30 or 2026-12-24T18:00."
        );
        assert_eq!(
            until_error_message(Strings::ENGLISH, &UntilError::Past),
            "That time has already passed."
        );
    }

    #[test]
    fn missing_values_name_the_command_or_option() {
        let message = |name: &str, expected| {
            cli_error_message(
                Strings::ENGLISH,
                &CliError::MissingValue {
                    name: name.into(),
                    expected,
                },
            )
        };
        assert_eq!(
            message("for", Expected::Duration),
            "for needs a duration, like 45m or 1h30m."
        );
        assert_eq!(
            message("until", Expected::Time),
            "until needs a time, like 17:30 or 2026-12-24T18:00."
        );
        assert_eq!(
            message("-e", Expected::Time),
            "-e needs a time, like 17:30 or 2026-12-24T18:00."
        );
        assert_eq!(
            message("--pid", Expected::ProcessId),
            "--pid needs a process ID, like --pid 1234."
        );
        assert_eq!(
            message("-t", Expected::Seconds),
            "-t needs a number of seconds, like -t 3600."
        );
    }

    #[test]
    fn bad_process_ids_are_sentences() {
        let message =
            |input: &str| cli_error_message(Strings::ENGLISH, &CliError::Pid { input: input.into() });
        assert_eq!(
            message("abc"),
            "\"abc\" isn't a process ID. Try a number like 1234."
        );
        assert_eq!(
            message("99999999999999999999"),
            "No running process has the ID 99999999999999999999."
        );
        assert_eq!(message("-5"), "No running process has the ID -5.");
        // One sign makes a number; more than one does not.
        assert_eq!(
            message("+-5"),
            "\"+-5\" isn't a process ID. Try a number like 1234."
        );
    }

    #[test]
    fn session_display() {
        let summary = SessionSummary {
            headline: "Keeping your PC awake for 1h 30m".into(),
            display_on: true,
            bound_to: Some("process 7 (cmd.exe)".into()),
        };
        let text = session_header(Strings::ENGLISH, Style::PLAIN, "v1", &summary);
        assert_eq!(
            text,
            "* PowerToys Awake v1\n\n● Keeping your PC awake for 1h 30m\n  └ Display stays on\n  └ Stops when process 7 (cmd.exe) exits\n\n"
        );
        assert_eq!(
            status_line(Strings::ENGLISH, Style::PLAIN, Some(5398)).trim_start(),
            "1:29:58 remaining · Ctrl+C to stop"
        );
        assert_eq!(
            status_line(Strings::ENGLISH, COLOR, None),
            "\r\x1b[2K  \x1b[2mCtrl+C to stop\x1b[0m"
        );
        assert!(farewell(Strings::ENGLISH, COLOR, ExitReason::TimeUp).starts_with("\r\x1b[2K\x1b[32m●"));
        assert_eq!(
            farewell(Strings::ENGLISH, Style::PLAIN, ExitReason::Requested),
            format!(
                "{}● Awake stopped. Your power plan applies again.\n",
                Style::PLAIN.clear_line()
            )
        );
    }

    #[test]
    fn every_exit_reason_says_why_the_session_ended() {
        let english = Strings::ENGLISH;
        let plain = |reason| farewell(english, Style::PLAIN, reason);
        for (reason, id) in [
            (ExitReason::Requested, StringId::CliStopped),
            (ExitReason::TimeUp, StringId::CliFinished),
            (ExitReason::TargetExited, StringId::CliTargetExited),
        ] {
            assert!(plain(reason).contains(english.get(id)), "{reason:?}");
        }
        // A failure reads as an error, not as a session that ended well.
        let failed = plain(ExitReason::PowerRequestFailed);
        assert!(failed.contains(english.get(StringId::CliErrPower)), "{failed}");
        assert!(failed.contains(english.get(StringId::CliError)), "{failed}");
    }

    #[test]
    fn width_counts_wide_characters_twice() {
        assert_eq!(display_width("abc"), 3);
        assert_eq!(display_width("時間"), 4);
    }
}
