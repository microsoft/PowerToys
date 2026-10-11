//! Durations typed by people (`1h30m`) and durations shown to them (`1:29:58`, `1h 30m`).

use std::fmt;

/// Why [`parse_duration`] rejected its input. People see the localized text from
/// `console::cli_error_message`; `Display` gives a neutral English message for logs.
#[derive(Clone, Debug, PartialEq, Eq)]
pub enum DurationError {
    /// Nothing but whitespace.
    Empty,
    /// A number without a unit, such as `90`; the unit is ambiguous.
    MissingUnit,
    /// Something other than numbers followed by d, h, m or s.
    Invalid,
    /// Adds up to no time at all, such as `0m`.
    Zero,
    /// Longer than [`MAX_SECONDS`].
    TooLong,
}

impl fmt::Display for DurationError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            DurationError::Empty => f.write_str("the duration is empty"),
            DurationError::MissingUnit => f.write_str("the duration needs a unit: d, h, m or s"),
            DurationError::Invalid => f.write_str("the duration is not numbers followed by d, h, m or s"),
            DurationError::Zero => f.write_str("the duration is zero"),
            DurationError::TooLong => write!(f, "the duration is longer than {}", Human(MAX_SECONDS)),
        }
    }
}

impl std::error::Error for DurationError {}

/// Longest duration accepted, matching the `uint` seconds the settings file can store.
pub const MAX_SECONDS: u64 = u32::MAX as u64;

/// Parses `45m`, `2h`, `1h30m`, `1h 30m`, `90s` or `1d`. Units: d, h, m, s.
pub fn parse_duration(text: &str) -> Result<u64, DurationError> {
    let compact: String = text
        .chars()
        .filter(|c| !c.is_whitespace())
        .collect::<String>()
        .to_ascii_lowercase();
    if compact.is_empty() {
        return Err(DurationError::Empty);
    }

    let mut total: u64 = 0;
    let mut number: Option<u64> = None;
    for c in compact.chars() {
        if let Some(digit) = c.to_digit(10) {
            let value = number
                .unwrap_or(0)
                .checked_mul(10)
                .and_then(|n| n.checked_add(u64::from(digit)));
            number = Some(value.ok_or(DurationError::TooLong)?);
            continue;
        }
        let unit_seconds = match c {
            'd' => 86_400,
            'h' => 3_600,
            'm' => 60,
            's' => 1,
            _ => return Err(DurationError::Invalid),
        };
        let value = number.take().ok_or(DurationError::Invalid)?;
        total = value
            .checked_mul(unit_seconds)
            .and_then(|seconds| total.checked_add(seconds))
            .ok_or(DurationError::TooLong)?;
    }
    if number.is_some() {
        return Err(DurationError::MissingUnit);
    }
    match total {
        0 => Err(DurationError::Zero),
        t if t > MAX_SECONDS => Err(DurationError::TooLong),
        t => Ok(t),
    }
}

/// Countdown text used in the tray tooltip: `H:MM:SS` from one hour up, `M:SS` below.
#[derive(Clone, Copy, Debug)]
pub struct Countdown(pub u64);

impl fmt::Display for Countdown {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        let (hours, minutes, seconds) = (self.0 / 3600, self.0 % 3600 / 60, self.0 % 60);
        if hours > 0 {
            write!(f, "{hours}:{minutes:02}:{seconds:02}")
        } else {
            write!(f, "{minutes}:{seconds:02}")
        }
    }
}

/// Compact human form such as `1h 30m`, `2d 4h` or `45s`, dropping zero parts.
#[derive(Clone, Copy, Debug)]
pub struct Human(pub u64);

impl fmt::Display for Human {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        let parts = [
            (self.0 / 86_400, 'd'),
            (self.0 % 86_400 / 3600, 'h'),
            (self.0 % 3600 / 60, 'm'),
            (self.0 % 60, 's'),
        ];
        let mut first = true;
        for (value, unit) in parts.into_iter().filter(|(value, _)| *value > 0) {
            if !first {
                f.write_str(" ")?;
            }
            write!(f, "{value}{unit}")?;
            first = false;
        }
        if first { f.write_str("0s") } else { Ok(()) }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn parses_common_forms() {
        assert_eq!(parse_duration("45m"), Ok(2700));
        assert_eq!(parse_duration("2h"), Ok(7200));
        assert_eq!(parse_duration("1h30m"), Ok(5400));
        assert_eq!(parse_duration(" 1H 30M "), Ok(5400));
        assert_eq!(parse_duration("90s"), Ok(90));
        assert_eq!(parse_duration("1d2h"), Ok(93_600));
    }

    #[test]
    fn rejects_bad_input() {
        assert_eq!(parse_duration(""), Err(DurationError::Empty));
        assert_eq!(parse_duration("90"), Err(DurationError::MissingUnit));
        assert_eq!(parse_duration("1h30"), Err(DurationError::MissingUnit));
        assert_eq!(parse_duration("h"), Err(DurationError::Invalid));
        assert_eq!(parse_duration("1x"), Err(DurationError::Invalid));
        assert_eq!(parse_duration("-5m"), Err(DurationError::Invalid));
        assert_eq!(parse_duration("0m"), Err(DurationError::Zero));
        assert_eq!(
            parse_duration("99999999999999999999s"),
            Err(DurationError::TooLong)
        );
        assert_eq!(parse_duration("50000d"), Err(DurationError::TooLong));
    }

    #[test]
    fn errors_describe_themselves() {
        assert_eq!(
            DurationError::MissingUnit.to_string(),
            "the duration needs a unit: d, h, m or s"
        );
        assert!(DurationError::TooLong.to_string().ends_with("49710d 6h 28m 15s"));
        let boxed: Box<dyn std::error::Error> = Box::new(DurationError::Zero);
        assert!(boxed.source().is_none());
    }

    #[test]
    fn countdown_matches_the_dotnet_format() {
        assert_eq!(Countdown(0).to_string(), "0:00");
        assert_eq!(Countdown(59).to_string(), "0:59");
        assert_eq!(Countdown(61).to_string(), "1:01");
        assert_eq!(Countdown(3600).to_string(), "1:00:00");
        assert_eq!(Countdown(5_000_000).to_string(), "1388:53:20");
    }

    #[test]
    fn human_form() {
        assert_eq!(Human(0).to_string(), "0s");
        assert_eq!(Human(45).to_string(), "45s");
        assert_eq!(Human(5400).to_string(), "1h 30m");
        assert_eq!(Human(93_601).to_string(), "1d 2h 1s");
    }
}
