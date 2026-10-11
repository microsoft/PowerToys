//! Command-line parsing.
//!
//! The modern surface is `PowerToys.Awake [for <duration> | until <time>] [-d] [--pid <pid> |
//! --parent]`. The original options (`-c`, `-t`, `-e`, `-u`, `-p`, `-d`) keep working, hidden
//! from help, because the PowerToys runner and existing scripts use them.

use std::ffi::OsString;
use std::fmt;

use clap::{ArgAction, Parser, Subcommand};

use crate::duration::{DurationError, parse_duration};
use crate::time::{ParsedTime, parse_time};

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
}

impl fmt::Display for CliError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            CliError::Usage(text) => f.write_str(text),
            CliError::Duration { input, error } => write!(f, "'{input}' is not a valid duration: {error}"),
            CliError::Time { input } => write!(f, "'{input}' is not a valid time"),
        }
    }
}

impl std::error::Error for CliError {
    fn source(&self) -> Option<&(dyn std::error::Error + 'static)> {
        match self {
            CliError::Duration { error, .. } => Some(error),
            CliError::Usage(_) | CliError::Time { .. } => None,
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

    #[allow(
        clippy::option_option,
        reason = "clap's shape for absent, given bare, or given a value"
    )]
    #[arg(short = 'p', long = "pid", num_args = 0..=1, allow_negative_numbers = true, global = true)]
    pid: Option<Option<i64>>,

    #[arg(long = "parent", action = ArgAction::SetTrue, global = true)]
    parent: bool,

    #[arg(short = 'v', long = "version", short_alias = 'V', action = ArgAction::SetTrue)]
    version: bool,

    #[arg(short = 'c', long = "use-pt-config", num_args = 0..=1, require_equals = true,
          default_missing_value = "true", value_parser = parse_bool, hide = true)]
    use_pt_config: Option<bool>,

    #[arg(short = 't', long = "time-limit", hide = true)]
    time_limit: Option<u32>,

    #[allow(
        clippy::option_option,
        reason = "clap's shape for absent, given bare, or given a value"
    )]
    #[arg(short = 'e', long = "expire-at", num_args = 0..=1, hide = true)]
    expire_at: Option<Option<String>>,

    #[arg(short = 'u', long = "use-parent-pid", num_args = 0..=1, require_equals = true,
          default_missing_value = "true", value_parser = parse_bool, hide = true)]
    use_parent_pid: Option<bool>,

    /// Internal: tells the caller that a handed-off session has started.
    #[arg(long = "handoff", hide = true, global = true)]
    handoff: Option<String>,
}

#[derive(Subcommand, Debug)]
enum Command {
    For {
        #[arg(required = true, num_args = 1..)]
        duration: Vec<String>,
    },
    Until {
        #[arg(required = true, num_args = 1..)]
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
    let args =
        Args::try_parse_from(join_legacy_booleans(tokens)).map_err(|e| CliError::Usage(summarize(&e)))?;
    if args.version {
        return Ok(Request::Version);
    }

    let legacy = args.use_pt_config.is_some()
        || args.time_limit.is_some()
        || args.expire_at.is_some()
        || args.use_parent_pid.is_some();
    let pid = args.pid.flatten().filter(|pid| *pid != 0);

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
        None => match (
            args.expire_at.flatten().filter(|text| !text.trim().is_empty()),
            args.time_limit,
        ) {
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

/// The original parser accepted `-c true`; clap needs `-c=true` to tell values from commands.
fn join_legacy_booleans(tokens: Vec<OsString>) -> Vec<OsString> {
    const FLAGS: [&str; 6] = [
        "-c",
        "--use-pt-config",
        "-d",
        "--display-on",
        "-u",
        "--use-parent-pid",
    ];
    let mut joined = Vec::with_capacity(tokens.len());
    let mut iter = tokens.into_iter().peekable();
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
    fn bare_or_zero_pid_means_unbound() {
        assert_eq!(run(&["--pid"]).binding, Binding::None);
        assert_eq!(run(&["--pid", "0"]).binding, Binding::None);
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
        assert!(matches!(error(&["--time-limit", "abc"]), CliError::Usage(_)));
        assert!(matches!(error(&["-t"]), CliError::Usage(_)));
        assert!(matches!(error(&["--bogus"]), CliError::Usage(_)));
        assert!(matches!(error(&["for"]), CliError::Usage(_)));
        assert_eq!(
            error(&["for", "90"]),
            CliError::Duration {
                input: "90".into(),
                error: DurationError::MissingUnit
            }
        );
        assert_eq!(error(&["until", "soon"]), CliError::Time { input: "soon".into() });
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
    }
}
