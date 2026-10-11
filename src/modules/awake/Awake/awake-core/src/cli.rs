//! Command-line parsing.
//!
//! The modern surface is `PowerToys.Awake [for <duration> | until <time>] [-d] [--pid <pid> |
//! --parent]`. The original options (`-c`, `-t`, `-e`, `-u`, `-p`, `-d`) keep working, hidden
//! from help, because the PowerToys runner and existing scripts use them.

use std::ffi::OsString;
use std::fmt;

use clap::error::{ContextKind, ContextValue, ErrorKind};
use clap::{ArgAction, Parser, Subcommand};

use crate::duration::{DurationError, parse_duration};
use crate::time::{CivilDateTime, DateTimeOffset, LocalZone, ParsedTime, parse_time};

/// The executable name, as shown in help and error text.
pub const PROGRAM: &str = "PowerToys.Awake";

/// What the command line asks for.
#[derive(Clone, Debug, PartialEq, Eq)]
pub enum Request {
    /// Print help and exit.
    Help,
    /// Print the version and exit.
    Version,
    /// Start a keep-awake session.
    Run(Invocation),
}

/// A keep-awake session as the command line describes it.
#[derive(Clone, Debug, PartialEq, Eq)]
pub struct Invocation {
    /// How long to keep the system awake.
    pub plan: Plan,
    /// Keep the display on as well as the system.
    pub display_on: bool,
    /// Which process, if any, the session ends with.
    pub binding: Binding,
    /// Started with the original options, which ran detached from the calling console.
    pub legacy: bool,
    /// Set on a process Awake started for itself: the event to signal once it has started.
    pub handoff_event: Option<String>,
}

/// How long a command-line session lasts.
#[derive(Clone, Debug, PartialEq, Eq)]
pub enum Plan {
    /// Follow `settings.json` (`--use-pt-config`).
    PowerToys,
    /// Until stopped (no command, or `-t 0`).
    Indefinite,
    /// `for <duration>` or `--time-limit`.
    For {
        /// Length of the session, at most [`crate::duration::MAX_SECONDS`].
        seconds: u64,
    },
    /// `until <time>` or `--expire-at`.
    Until(UntilSpec),
}

/// The end of an `until` session, before it is resolved against the local time zone.
#[derive(Clone, Debug, PartialEq, Eq)]
pub enum UntilSpec {
    /// A time Awake parsed itself.
    Parsed(ParsedTime),
    /// `--expire-at` text that is not ISO 8601, such as `10/11/2026 5:00 PM`; the platform layer
    /// parses it with the user's regional format, as the .NET version did.
    Text(String),
}

/// Why [`resolve_until`] turned a time down.
#[derive(Clone, Debug, PartialEq, Eq)]
pub enum UntilError {
    /// Not a time Awake can read, or a local time that does not exist, such as one inside a
    /// daylight saving gap. Holds the time as the user should see it.
    Unreadable(String),
    /// The time has already passed.
    Past,
}

/// When an `until` session ends. A time of day means its next occurrence: later today, else
/// tomorrow. `zone` turns local times into instants, `regional` reads `--expire-at` text in the
/// user's regional format, and `now` is the current time.
///
/// # Errors
///
/// An [`UntilError`] for a time that cannot be resolved or that has already passed.
pub fn resolve_until(
    spec: &UntilSpec,
    zone: &dyn LocalZone,
    regional: &dyn Fn(&str) -> Option<CivilDateTime>,
    now: DateTimeOffset,
) -> Result<DateTimeOffset, UntilError> {
    let until = match spec {
        UntilSpec::Parsed(ParsedTime::Absolute(at)) => *at,
        UntilSpec::Parsed(ParsedTime::Local(local)) => zone
            .resolve(*local)
            .ok_or_else(|| UntilError::Unreadable(civil_text(local)))?,
        UntilSpec::Parsed(ParsedTime::TimeOfDay { hour, minute, second }) => {
            let today = now.local();
            let at = |date: CivilDateTime| {
                CivilDateTime::new(date.year, date.month, date.day, *hour, *minute, *second)
                    .and_then(|local| zone.resolve(local))
            };
            match at(today) {
                Some(candidate) if candidate.utc > now.utc => candidate,
                _ => {
                    let tomorrow =
                        CivilDateTime::from_timestamp(today.to_timestamp().saturating_add_secs(86_400));
                    at(tomorrow).ok_or_else(|| UntilError::Unreadable(clock_text(*hour, *minute, *second)))?
                }
            }
        }
        UntilSpec::Text(input) => regional(input)
            .and_then(|local| zone.resolve(local))
            .ok_or_else(|| UntilError::Unreadable(input.clone()))?,
    };
    if until.utc <= now.utc {
        return Err(UntilError::Past);
    }
    Ok(until)
}

/// `2026-03-29 02:30`, the way the user could have typed it.
fn civil_text(local: &CivilDateTime) -> String {
    format!(
        "{:04}-{:02}-{:02} {}",
        local.year,
        local.month,
        local.day,
        clock_text(local.hour, local.minute, local.second)
    )
}

fn clock_text(hour: u8, minute: u8, second: u8) -> String {
    if second == 0 {
        format!("{hour:02}:{minute:02}")
    } else {
        format!("{hour:02}:{minute:02}:{second:02}")
    }
}

/// A process whose exit ends the session.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Binding {
    /// The session ends on its own terms.
    None,
    /// `--pid`: this process. Kept signed so negative input can be rejected with a clear message.
    Pid(i64),
    /// `--parent`: the process that started Awake.
    Parent,
}

/// Why the command line was rejected. People see the localized text from
/// `text::cli_error_message`; `Display` gives a neutral English message for logs.
#[derive(Clone, Debug, PartialEq, Eq)]
pub enum CliError {
    /// Unknown options, missing values and similar; the text comes from clap.
    Usage(String),
    /// The argument of `for` is not a duration.
    Duration {
        /// The text as typed, arguments joined by spaces.
        input: String,
        /// What is wrong with it.
        error: DurationError,
    },
    /// The argument of `until` is not a time Awake understands.
    Time {
        /// The text as typed, arguments joined by spaces.
        input: String,
    },
    /// The value of `--pid` is not a number that fits a process ID.
    Pid {
        /// The text as typed.
        input: String,
    },
    /// The value of `--time-limit` is not a whole number of seconds.
    Seconds {
        /// The text as typed.
        input: String,
    },
    /// A command or option that takes a value was given none.
    MissingValue {
        /// The command or option as typed, such as `for` or `-e`.
        name: String,
        /// What it needs.
        expected: Expected,
    },
}

/// The kind of value a command or option needs.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Expected {
    /// A duration, for `for`.
    Duration,
    /// A time, for `until` and `--expire-at`.
    Time,
    /// A process ID, for `--pid`.
    ProcessId,
    /// A whole number of seconds, for `--time-limit`.
    Seconds,
}

impl fmt::Display for CliError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            CliError::Usage(text) => f.write_str(text),
            CliError::Duration { input, error } => write!(f, "'{input}' is not a valid duration: {error}"),
            CliError::Time { input } => write!(f, "'{input}' is not a valid time"),
            CliError::Pid { input } => write!(f, "'{input}' is not a valid process ID"),
            CliError::Seconds { input } => write!(f, "'{input}' is not a whole number of seconds"),
            CliError::MissingValue { name, expected } => {
                let what = match expected {
                    Expected::Duration => "a duration",
                    Expected::Time => "a time",
                    Expected::ProcessId => "a process ID",
                    Expected::Seconds => "a number of seconds",
                };
                write!(f, "{name} needs {what}")
            }
        }
    }
}

impl std::error::Error for CliError {
    fn source(&self) -> Option<&(dyn std::error::Error + 'static)> {
        match self {
            CliError::Duration { error, .. } => Some(error),
            CliError::Usage(_)
            | CliError::Time { .. }
            | CliError::Pid { .. }
            | CliError::Seconds { .. }
            | CliError::MissingValue { .. } => None,
        }
    }
}

#[derive(Parser, Debug)]
#[command(
    name = PROGRAM,
    disable_help_flag = true,
    disable_version_flag = true,
    disable_help_subcommand = true,
    args_override_self = true
)]
struct Args {
    #[command(subcommand)]
    command: Option<Command>,

    #[arg(short = 'd', long = "display-on", num_args = 0..=1, require_equals = true,
          default_missing_value = "true", value_parser = parse_bool, global = true)]
    display_on: Option<bool>,

    // Text, so a value that is not a number gets Awake's own message rather than clap's.
    // `missing_option_value` has already turned away option-shaped values, so a hyphen here
    // starts a signed value.
    #[arg(short = 'p', long = "pid", allow_hyphen_values = true, global = true)]
    pid: Option<String>,

    #[arg(long = "parent", action = ArgAction::SetTrue, global = true)]
    parent: bool,

    #[arg(short = 'v', long = "version", short_alias = 'V', action = ArgAction::SetTrue)]
    version: bool,

    #[arg(short = 'c', long = "use-pt-config", num_args = 0..=1, require_equals = true,
          default_missing_value = "true", value_parser = parse_bool, hide = true)]
    use_pt_config: Option<bool>,

    // Text for the same reason as `pid`: a value that is not a number gets Awake's own message.
    #[arg(short = 't', long = "time-limit", allow_hyphen_values = true, hide = true)]
    time_limit: Option<String>,

    #[arg(short = 'e', long = "expire-at", allow_hyphen_values = true, hide = true)]
    expire_at: Option<String>,

    #[arg(short = 'u', long = "use-parent-pid", num_args = 0..=1, require_equals = true,
          default_missing_value = "true", value_parser = parse_bool, hide = true)]
    use_parent_pid: Option<bool>,

    /// Internal: tells the caller that a handed-off session has started.
    #[arg(long = "handoff", hide = true, global = true)]
    handoff: Option<String>,
}

// The arguments are optional to clap so that leaving one out gets a localized sentence naming
// the command; clap's own message for it ends in a list of arguments Awake does not show.
#[derive(Subcommand, Debug)]
enum Command {
    For {
        #[arg(num_args = 0..)]
        duration: Vec<String>,
    },
    Until {
        #[arg(num_args = 0..)]
        time: Vec<String>,
    },
}

/// Parses the full argument list, program name first.
///
/// # Errors
///
/// A [`CliError`] for arguments Awake does not accept, ready to show to the user.
pub fn parse<I, T>(args: I) -> Result<Request, CliError>
where
    I: IntoIterator<Item = T>,
    T: Into<OsString>,
{
    let tokens: Vec<OsString> = args.into_iter().map(Into::into).collect();
    // Help wins wherever it appears, even next to mistakes, as it always has.
    if tokens.iter().skip(1).any(is_help_token) {
        return Ok(Request::Help);
    }
    let tokens = normalize_legacy(tokens);
    if let Some(error) = missing_option_value(&tokens) {
        return Err(error);
    }
    let args = Args::try_parse_from(&tokens)
        .map_err(|e| signed_value(&e, &tokens).unwrap_or_else(|| CliError::Usage(summarize(&e))))?;
    if args.version {
        return Ok(Request::Version);
    }

    let legacy = args.use_pt_config.is_some()
        || args.time_limit.is_some()
        || args.expire_at.is_some()
        || args.use_parent_pid.is_some();
    // `--pid 0` is how a caller says "no process", as the .NET version allowed.
    let pid = match args.pid {
        Some(text) => match text.trim().parse::<i64>() {
            Ok(0) => None,
            Ok(pid) => Some(pid),
            Err(_) => return Err(CliError::Pid { input: text }),
        },
        None => None,
    };

    let time_limit = match args.time_limit {
        Some(text) => match text.trim().parse::<u32>() {
            Ok(seconds) => Some(seconds),
            Err(_) => return Err(CliError::Seconds { input: text }),
        },
        None => None,
    };

    if args.use_pt_config == Some(true) {
        // Settings come from the file; only --pid still applies.
        let binding = pid.map_or(Binding::None, Binding::Pid);
        return Ok(Request::Run(Invocation {
            plan: Plan::PowerToys,
            display_on: false,
            binding,
            legacy,
            handoff_event: args.handoff,
        }));
    }

    let binding = match pid {
        Some(pid) => Binding::Pid(pid),
        None if args.parent || args.use_parent_pid == Some(true) => Binding::Parent,
        None => Binding::None,
    };

    let plan = match args.command {
        Some(Command::For { duration }) if duration.is_empty() => {
            return Err(CliError::MissingValue {
                name: "for".into(),
                expected: Expected::Duration,
            });
        }
        Some(Command::Until { time }) if time.is_empty() => {
            return Err(CliError::MissingValue {
                name: "until".into(),
                expected: Expected::Time,
            });
        }
        Some(Command::For { duration }) => {
            let input = duration.join(" ");
            match parse_duration(&input) {
                Ok(seconds) => Plan::For { seconds },
                Err(error) => return Err(CliError::Duration { input, error }),
            }
        }
        Some(Command::Until { time }) => {
            let input = time.join(" ");
            match parse_time(&input) {
                Some(parsed) => Plan::Until(UntilSpec::Parsed(parsed)),
                None => return Err(CliError::Time { input }),
            }
        }
        None => match (args.expire_at, time_limit) {
            (Some(text), _) => {
                Plan::Until(parse_time(&text).map_or(UntilSpec::Text(text), UntilSpec::Parsed))
            }
            (None, Some(seconds)) if seconds > 0 => Plan::For {
                seconds: seconds.into(),
            },
            _ => Plan::Indefinite,
        },
    };

    Ok(Request::Run(Invocation {
        plan,
        display_on: args.display_on.unwrap_or(false),
        binding,
        legacy,
        handoff_event: args.handoff,
    }))
}

fn is_help_token(token: &OsString) -> bool {
    token.to_str().is_some_and(|t| {
        ["-h", "--help", "-?", "/?", "/h"]
            .iter()
            .any(|h| t.eq_ignore_ascii_case(h))
    })
}

/// Options the .NET version took; it accepted `-t:60` as well as `-t=60` and `-t 60`.
const LEGACY_OPTIONS: [&str; 12] = [
    "-c",
    "--use-pt-config",
    "-d",
    "--display-on",
    "-t",
    "--time-limit",
    "-p",
    "--pid",
    "-e",
    "--expire-at",
    "-u",
    "--use-parent-pid",
];

/// Rewrites the spellings the .NET parser accepted into ones clap understands: `-t:60` becomes
/// `-t=60`, and `-c true` becomes `-c=true`, since clap needs the `=` to tell a boolean's value
/// from a command.
fn normalize_legacy(tokens: Vec<OsString>) -> Vec<OsString> {
    const FLAGS: [&str; 6] = [
        "-c",
        "--use-pt-config",
        "-d",
        "--display-on",
        "-u",
        "--use-parent-pid",
    ];
    let mut joined = Vec::with_capacity(tokens.len());
    let mut iter = tokens.into_iter().map(colon_to_equals).peekable();
    while let Some(token) = iter.next() {
        let is_flag = token.to_str().is_some_and(|t| FLAGS.contains(&t));
        let next_is_bool = iter
            .peek()
            .and_then(|next| next.to_str())
            .is_some_and(|next| next.eq_ignore_ascii_case("true") || next.eq_ignore_ascii_case("false"));
        if is_flag && next_is_bool {
            let mut merged = token;
            merged.push("=");
            merged.push(iter.next().unwrap_or_default());
            joined.push(merged);
        } else {
            joined.push(token);
        }
    }
    joined
}

fn colon_to_equals(token: OsString) -> OsString {
    let rewritten = token.to_str().and_then(|text| {
        LEGACY_OPTIONS.iter().find_map(|name| {
            let value = text.strip_prefix(name)?.strip_prefix(':')?;
            Some(OsString::from(format!("{name}={value}")))
        })
    });
    rewritten.unwrap_or(token)
}

/// `--pid`, `--expire-at` or `--time-limit` with nothing usable after it. Checked before clap
/// sees the tokens, because clap would take a following `for` as the value and then report the
/// duration as an unknown command.
fn missing_option_value(tokens: &[OsString]) -> Option<CliError> {
    let mut iter = tokens.iter().skip(1).map(|token| token.to_str());
    while let Some(token) = iter.next() {
        let Some(token) = token else { continue };
        let (name, inline) = match token.split_once('=') {
            Some((name, value)) => (name, Some(value)),
            None => (token, None),
        };
        let expected = match name {
            "-p" | "--pid" => Expected::ProcessId,
            "-e" | "--expire-at" => Expected::Time,
            "-t" | "--time-limit" => Expected::Seconds,
            _ => continue,
        };
        let value = match inline {
            Some(value) => Some(value),
            None => iter.clone().next().flatten(),
        };
        // A signed value such as -5 is still a value, so it gets the message about a bad ID or
        // time rather than one saying nothing was typed.
        let usable = value.is_some_and(|value| {
            !value.trim().is_empty()
                && !matches!(value, "for" | "until")
                && (!value.starts_with('-') || is_signed_value(value))
        });
        if !usable {
            return Some(CliError::MissingValue {
                name: name.to_owned(),
                expected,
            });
        }
    }
    None
}

/// No Awake option starts with a digit, so `-5m` or `-05:00` is a value with a sign, not an
/// option.
fn is_signed_value(token: &str) -> bool {
    token
        .strip_prefix('-')
        .is_some_and(|rest| rest.starts_with(|c: char| c.is_ascii_digit()))
}

/// clap reads `for -5m` as an unknown option `-5`. It is a signed value meant for `for` or
/// `until`, and gets the same message as any other bad value, quoting the whole value as the
/// user typed it.
fn signed_value(error: &clap::Error, tokens: &[OsString]) -> Option<CliError> {
    if error.kind() != ErrorKind::UnknownArgument {
        return None;
    }
    let Some(ContextValue::String(unknown)) = error.get(ContextKind::InvalidArg) else {
        return None;
    };
    if !is_signed_value(unknown) {
        return None;
    }
    let (command, input) = command_value(tokens)?;
    Some(if command == "for" {
        // A leading minus is never part of a duration.
        CliError::Duration {
            input,
            error: DurationError::Invalid,
        }
    } else {
        CliError::Time { input }
    })
}

/// The command and its value as clap would have collected it: every later token except options
/// and the values they take.
fn command_value(tokens: &[OsString]) -> Option<(&str, String)> {
    let mut rest = tokens.iter().skip(1).filter_map(|token| token.to_str());
    let command = rest.find(|token| matches!(*token, "for" | "until"))?;
    let mut words = Vec::new();
    while let Some(token) = rest.next() {
        if !token.starts_with('-') || is_signed_value(token) {
            words.push(token);
        } else if matches!(token, "-p" | "--pid" | "--handoff") {
            rest.next();
        }
    }
    Some((command, words.join(" ")))
}

fn parse_bool(text: &str) -> Result<bool, String> {
    match text.to_ascii_lowercase().as_str() {
        "true" => Ok(true),
        "false" => Ok(false),
        _ => Err(format!("expected true or false, got '{text}'")),
    }
}

/// clap's first line plus any "tip:" line, without the `error:` prefix or the usage block.
fn summarize(error: &clap::Error) -> String {
    let rendered = error.to_string();
    let mut lines = rendered.lines().filter(|line| !line.trim().is_empty());
    let mut summary = lines
        .next()
        .unwrap_or_default()
        .trim_start_matches("error: ")
        .to_owned();
    for tip in lines.filter(|line| line.trim_start().starts_with("tip:")) {
        summary.push('\n');
        summary.push_str(tip.trim());
    }
    summary
}

#[cfg(test)]
mod tests {
    use super::*;

    fn run(args: &[&str]) -> Invocation {
        let mut all = vec![PROGRAM];
        all.extend_from_slice(args);
        match parse(all) {
            Ok(Request::Run(invocation)) => invocation,
            other => panic!("{args:?}: {other:?}"),
        }
    }

    fn error(args: &[&str]) -> CliError {
        let mut all = vec![PROGRAM];
        all.extend_from_slice(args);
        parse(all).unwrap_err()
    }

    #[test]
    fn no_arguments_keeps_awake_indefinitely() {
        let invocation = run(&[]);
        assert_eq!(invocation.plan, Plan::Indefinite);
        assert!(!invocation.display_on && !invocation.legacy);
        assert_eq!(invocation.binding, Binding::None);
    }

    #[test]
    fn runner_command_line() {
        let invocation = run(&["--use-pt-config", "--pid", "4242"]);
        assert_eq!(invocation.plan, Plan::PowerToys);
        assert_eq!(invocation.binding, Binding::Pid(4242));
    }

    #[test]
    fn powertoys_mode_ignores_other_options_but_pid() {
        let invocation = run(&["-c", "-t", "60", "-u", "-d"]);
        assert_eq!(
            (invocation.plan, invocation.binding, invocation.display_on),
            (Plan::PowerToys, Binding::None, false)
        );
    }

    #[test]
    fn modern_commands() {
        assert_eq!(run(&["for", "1h30m"]).plan, Plan::For { seconds: 5400 });
        assert_eq!(run(&["for", "1h", "30m"]).plan, Plan::For { seconds: 5400 });
        assert!(matches!(
            run(&["until", "17:30"]).plan,
            Plan::Until(UntilSpec::Parsed(ParsedTime::TimeOfDay { .. }))
        ));
        assert!(matches!(
            run(&["until", "2026-12-24", "18:00"]).plan,
            Plan::Until(UntilSpec::Parsed(ParsedTime::Local(_)))
        ));
    }

    #[test]
    fn options_work_before_and_after_commands() {
        let before = run(&["-d", "for", "45m", "--parent"]);
        let after = run(&["for", "45m", "--display-on", "--parent"]);
        assert_eq!(before, after);
        assert!(before.display_on);
        assert_eq!(before.binding, Binding::Parent);
    }

    #[test]
    fn legacy_options_still_work() {
        assert_eq!(run(&["-t", "600"]).plan, Plan::For { seconds: 600 });
        assert_eq!(run(&["--time-limit", "0"]).plan, Plan::Indefinite);
        assert!(run(&["-t", "600"]).legacy);
        assert_eq!(run(&["-u"]).binding, Binding::Parent);
        assert_eq!(run(&["-p", "77"]).binding, Binding::Pid(77));
        assert!(matches!(
            run(&["-e", "2026-12-24T18:00:00+01:00"]).plan,
            Plan::Until(UntilSpec::Parsed(ParsedTime::Absolute(_)))
        ));
        assert_eq!(
            run(&["-e", "12/24/2026 6:00 PM"]).plan,
            Plan::Until(UntilSpec::Text("12/24/2026 6:00 PM".into()))
        );
    }

    #[test]
    fn expire_at_wins_over_time_limit() {
        assert!(matches!(run(&["-t", "60", "-e", "18:00"]).plan, Plan::Until(_)));
    }

    #[test]
    fn explicit_boolean_values_are_accepted() {
        assert!(run(&["-d", "true", "-t", "5"]).display_on);
        assert!(!run(&["--display-on", "FALSE"]).display_on);
        assert_eq!(run(&["-c", "false", "-t", "5"]).plan, Plan::For { seconds: 5 });
        assert!(run(&["-d=true"]).display_on);
    }

    #[test]
    fn legacy_colon_separator_still_works() {
        assert_eq!(run(&["-t:60"]).plan, Plan::For { seconds: 60 });
        assert_eq!(run(&["--time-limit:60"]).plan, Plan::For { seconds: 60 });
        assert_eq!(run(&["-c:true"]).plan, Plan::PowerToys);
        assert_eq!(
            run(&["--use-pt-config:false", "-t:5"]).plan,
            Plan::For { seconds: 5 }
        );
        assert!(run(&["-d:true"]).display_on);
        assert!(!run(&["--display-on:false"]).display_on);
        assert_eq!(run(&["-u:true"]).binding, Binding::Parent);
        assert_eq!(run(&["--use-parent-pid:false"]).binding, Binding::None);
        assert_eq!(run(&["-p:77"]).binding, Binding::Pid(77));
        assert_eq!(run(&["--pid:77"]).binding, Binding::Pid(77));
        // Only the first colon separates; the rest belong to the time.
        assert!(matches!(
            run(&["-e:2026-12-24T18:00:00+01:00"]).plan,
            Plan::Until(UntilSpec::Parsed(ParsedTime::Absolute(_)))
        ));
        assert!(matches!(
            run(&["--expire-at:18:00"]).plan,
            Plan::Until(UntilSpec::Parsed(ParsedTime::TimeOfDay { hour: 18, .. }))
        ));
    }

    #[test]
    fn colons_elsewhere_are_left_alone() {
        assert!(matches!(
            run(&["until", "17:30"]).plan,
            Plan::Until(UntilSpec::Parsed(ParsedTime::TimeOfDay { .. }))
        ));
        assert!(matches!(error(&["--parent:true"]), CliError::Usage(_)));
    }

    #[test]
    fn zero_pid_means_unbound() {
        assert_eq!(run(&["--pid", "0"]).binding, Binding::None);
        assert_eq!(run(&["-c", "--pid=0"]).binding, Binding::None);
        assert_eq!(
            run(&["--pid", "-5"]).binding,
            Binding::Pid(-5),
            "rejected later with a clear message"
        );
    }

    #[test]
    fn help_anywhere() {
        for args in [
            vec!["-h"],
            vec!["--HELP"],
            vec!["-?"],
            vec!["--time-limit", "abc", "--help"],
            vec!["for", "-h"],
        ] {
            let mut all = vec![PROGRAM];
            all.extend(args.iter());
            assert_eq!(parse(all), Ok(Request::Help), "{args:?}");
        }
    }

    #[test]
    fn handoff_marker_works_after_commands() {
        let invocation = run(&["for", "60s", "--pid", "7", "--handoff", r"Local\x"]);
        assert_eq!(invocation.handoff_event.as_deref(), Some(r"Local\x"));
        assert_eq!(invocation.binding, Binding::Pid(7));
        assert!(!invocation.legacy);
    }

    #[test]
    fn version() {
        assert_eq!(parse([PROGRAM, "--version"]), Ok(Request::Version));
        assert_eq!(parse([PROGRAM, "-v"]), Ok(Request::Version));
    }

    #[test]
    fn mistakes_are_reported() {
        assert!(matches!(error(&["--bogus"]), CliError::Usage(_)));
        assert_eq!(
            error(&["for", "90"]),
            CliError::Duration {
                input: "90".into(),
                error: DurationError::MissingUnit
            }
        );
        assert_eq!(error(&["until", "soon"]), CliError::Time { input: "soon".into() });
    }

    fn missing(name: &str, expected: Expected) -> CliError {
        CliError::MissingValue {
            name: name.into(),
            expected,
        }
    }

    #[test]
    fn commands_without_a_value_name_what_is_missing() {
        assert_eq!(error(&["for"]), missing("for", Expected::Duration));
        assert_eq!(error(&["for", "-d"]), missing("for", Expected::Duration));
        assert_eq!(error(&["until"]), missing("until", Expected::Time));
        assert_eq!(error(&["until", "--parent"]), missing("until", Expected::Time));
    }

    #[test]
    fn bare_expire_at_is_an_error_not_an_indefinite_session() {
        assert_eq!(error(&["-e"]), missing("-e", Expected::Time));
        assert_eq!(error(&["--expire-at"]), missing("--expire-at", Expected::Time));
        assert_eq!(error(&["-e", "-d"]), missing("-e", Expected::Time));
        assert_eq!(error(&["-e", "--pid", "5"]), missing("-e", Expected::Time));
        assert_eq!(error(&["-e="]), missing("-e", Expected::Time));
        assert_eq!(error(&["-e:"]), missing("-e", Expected::Time));
        assert_eq!(
            error(&["--expire-at", "  "]),
            missing("--expire-at", Expected::Time)
        );
    }

    #[test]
    fn signed_expire_at_is_a_bad_time_not_a_missing_one() {
        // The shell reports text it cannot read as a time, naming what was typed.
        assert_eq!(run(&["-e", "-5"]).plan, Plan::Until(UntilSpec::Text("-5".into())));
        assert_eq!(
            run(&["--expire-at=-05:00"]).plan,
            Plan::Until(UntilSpec::Text("-05:00".into()))
        );
    }

    #[test]
    fn time_limits_that_are_not_whole_seconds_get_their_own_error() {
        let seconds = |input: &str| CliError::Seconds { input: input.into() };
        assert_eq!(error(&["--time-limit", "abc"]), seconds("abc"));
        assert_eq!(error(&["-t", "45m"]), seconds("45m"));
        assert_eq!(error(&["-t:1.5"]), seconds("1.5"));
        assert_eq!(error(&["-t", "-5"]), seconds("-5"));
        assert_eq!(error(&["-t", "4294967296"]), seconds("4294967296"));
        // Checked even where the value would be ignored, as --pid is.
        assert_eq!(error(&["-c", "-t", "abc"]), seconds("abc"));
        assert_eq!(run(&["-t", " 90 "]).plan, Plan::For { seconds: 90 });
    }

    #[test]
    fn bare_time_limit_is_an_error_and_keeps_the_command() {
        assert_eq!(error(&["-t"]), missing("-t", Expected::Seconds));
        assert_eq!(
            error(&["--time-limit="]),
            missing("--time-limit", Expected::Seconds)
        );
        assert_eq!(error(&["-t", "-d"]), missing("-t", Expected::Seconds));
        assert_eq!(error(&["-t", "for", "2s"]), missing("-t", Expected::Seconds));
    }

    #[test]
    fn bare_pid_is_an_error_and_keeps_the_command() {
        assert_eq!(error(&["--pid"]), missing("--pid", Expected::ProcessId));
        assert_eq!(error(&["-p"]), missing("-p", Expected::ProcessId));
        assert_eq!(error(&["--pid="]), missing("--pid", Expected::ProcessId));
        assert_eq!(
            error(&["--pid", "--parent"]),
            missing("--pid", Expected::ProcessId)
        );
        // Not "unrecognized subcommand '2s'": `for` is the command, not the ID.
        assert_eq!(
            error(&["--pid", "for", "2s"]),
            missing("--pid", Expected::ProcessId)
        );
        assert_eq!(
            error(&["-p", "until", "18:00"]),
            missing("-p", Expected::ProcessId)
        );
        assert_eq!(
            error(&["for", "2s", "--pid"]),
            missing("--pid", Expected::ProcessId)
        );
    }

    #[test]
    fn process_ids_that_are_not_numbers_get_their_own_error() {
        assert_eq!(error(&["--pid", "abc"]), CliError::Pid { input: "abc".into() });
        assert_eq!(
            error(&["--pid", "99999999999999999999"]),
            CliError::Pid {
                input: "99999999999999999999".into()
            }
        );
        assert_eq!(
            error(&["-c", "--pid", "1.5"]),
            CliError::Pid { input: "1.5".into() }
        );
        // A signed value was typed, so it is a bad ID rather than a missing one.
        assert_eq!(error(&["--pid", "-5x"]), CliError::Pid { input: "-5x".into() });
        assert_eq!(error(&["--pid=+-5"]), CliError::Pid { input: "+-5".into() });
    }

    #[test]
    fn signed_values_get_the_value_error_not_an_unknown_option() {
        assert_eq!(
            error(&["for", "-5m"]),
            CliError::Duration {
                input: "-5m".into(),
                error: DurationError::Invalid
            }
        );
        assert_eq!(
            error(&["-d", "for", "-5m", "--parent"]),
            CliError::Duration {
                input: "-5m".into(),
                error: DurationError::Invalid
            }
        );
        // The whole value is quoted, not just the signed piece clap stopped at, and options
        // between its words are left out as they are for any other value.
        assert_eq!(
            error(&["for", "1h", "-5m"]),
            CliError::Duration {
                input: "1h -5m".into(),
                error: DurationError::Invalid
            }
        );
        assert_eq!(
            error(&["for", "1h", "--pid", "42", "-d", "-5m", "2s"]),
            CliError::Duration {
                input: "1h -5m 2s".into(),
                error: DurationError::Invalid
            }
        );
        assert_eq!(
            error(&["until", "2026-12-24", "18:00", "-05:00"]),
            CliError::Time {
                input: "2026-12-24 18:00 -05:00".into()
            }
        );
        assert_eq!(error(&["until", "-5"]), CliError::Time { input: "-5".into() });
        // Without a command it is still an unknown option.
        assert!(matches!(error(&["-5m"]), CliError::Usage(_)));
    }

    #[test]
    fn usage_errors_are_one_short_paragraph() {
        let CliError::Usage(text) = error(&["--displayon"]) else {
            panic!()
        };
        assert!(!text.contains("Usage:"), "{text}");
        assert!(!text.starts_with("error:"), "{text}");
        assert!(text.contains("--display-on"), "suggests the right option: {text}");
    }

    #[test]
    fn errors_work_as_standard_errors() {
        use std::error::Error as _;

        let duration = error(&["for", "90"]);
        assert_eq!(
            duration.to_string(),
            "'90' is not a valid duration: the duration needs a unit: d, h, m or s"
        );
        assert_eq!(
            duration.source().map(ToString::to_string),
            Some(DurationError::MissingUnit.to_string())
        );
        let time = error(&["until", "soon"]);
        assert_eq!(time.to_string(), "'soon' is not a valid time");
        assert!(time.source().is_none());
        let boxed: Box<dyn std::error::Error> = Box::new(CliError::Usage("bad".into()));
        assert_eq!(boxed.to_string(), "bad");
        assert_eq!(
            missing("for", Expected::Duration).to_string(),
            "for needs a duration"
        );
        assert_eq!(
            CliError::Pid { input: "abc".into() }.to_string(),
            "'abc' is not a valid process ID"
        );
        assert_eq!(
            CliError::Seconds { input: "abc".into() }.to_string(),
            "'abc' is not a whole number of seconds"
        );
    }

    mod until {
        use super::super::*;
        use crate::time::FixedZone;

        const ZONE: FixedZone = FixedZone(60);

        fn local(day: u8, hour: u8, minute: u8, second: u8) -> DateTimeOffset {
            ZONE.resolve(CivilDateTime::new(2026, 3, day, hour, minute, second).unwrap())
                .unwrap()
        }

        fn no_regional(_: &str) -> Option<CivilDateTime> {
            None
        }

        fn resolve(spec: &UntilSpec, now: DateTimeOffset) -> Result<DateTimeOffset, UntilError> {
            resolve_until(spec, &ZONE, &no_regional, now)
        }

        fn time_of_day(hour: u8, minute: u8) -> UntilSpec {
            UntilSpec::Parsed(ParsedTime::TimeOfDay {
                hour,
                minute,
                second: 0,
            })
        }

        #[test]
        fn a_time_of_day_means_the_next_one() {
            let now = local(28, 10, 30, 15);
            assert_eq!(resolve(&time_of_day(18, 0), now), Ok(local(28, 18, 0, 0)));
            assert_eq!(resolve(&time_of_day(10, 30), now), Ok(local(29, 10, 30, 0)));
            // Not later than now, so tomorrow's.
            assert_eq!(
                resolve(&time_of_day(10, 30), local(28, 10, 30, 0)),
                Ok(local(29, 10, 30, 0))
            );
        }

        #[test]
        fn past_times_are_refused() {
            let now = local(28, 10, 0, 0);
            let past = UntilSpec::Parsed(ParsedTime::Absolute(local(28, 9, 0, 0)));
            assert_eq!(resolve(&past, now), Err(UntilError::Past));
            let exactly_now = UntilSpec::Parsed(ParsedTime::Absolute(now));
            assert_eq!(resolve(&exactly_now, now), Err(UntilError::Past));
            let local_past = UntilSpec::Parsed(ParsedTime::Local(
                CivilDateTime::new(2026, 3, 27, 12, 0, 0).unwrap(),
            ));
            assert_eq!(resolve(&local_past, now), Err(UntilError::Past));
        }

        #[test]
        fn regional_text_is_read_by_the_platform() {
            let now = local(28, 10, 0, 0);
            let regional = |text: &str| {
                (text == "28.03.2026 18:00").then(|| CivilDateTime::new(2026, 3, 28, 18, 0, 0).unwrap())
            };
            let spec = UntilSpec::Text("28.03.2026 18:00".into());
            assert_eq!(
                resolve_until(&spec, &ZONE, &regional, now),
                Ok(local(28, 18, 0, 0))
            );
            let soonish = UntilSpec::Text("soonish".into());
            assert_eq!(
                resolve_until(&soonish, &ZONE, &regional, now),
                Err(UntilError::Unreadable("soonish".into()))
            );
        }

        /// A zone where 02:00 to 03:00 does not exist, like the night clocks go forward.
        struct Gap;

        impl LocalZone for Gap {
            fn resolve(&self, local: CivilDateTime) -> Option<DateTimeOffset> {
                (local.hour != 2).then(|| ZONE.resolve(local)).flatten()
            }
        }

        #[test]
        fn local_times_that_do_not_exist_are_named_as_typed() {
            let now = local(28, 10, 0, 0);
            let skipped = UntilSpec::Parsed(ParsedTime::Local(
                CivilDateTime::new(2026, 3, 29, 2, 30, 0).unwrap(),
            ));
            assert_eq!(
                resolve_until(&skipped, &Gap, &no_regional, now),
                Err(UntilError::Unreadable("2026-03-29 02:30".into()))
            );
            let tonight = UntilSpec::Parsed(ParsedTime::TimeOfDay {
                hour: 2,
                minute: 30,
                second: 5,
            });
            assert_eq!(
                resolve_until(&tonight, &Gap, &no_regional, now),
                Err(UntilError::Unreadable("02:30:05".into()))
            );
        }
    }
}
