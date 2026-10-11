//! Points in time as Windows FILETIME ticks, plus the ISO 8601 text that .NET writes for
//! `DateTimeOffset` values in `settings.json`.

use std::fmt::Write as _;

/// FILETIME ticks are 100 ns.
pub const TICKS_PER_SECOND: i64 = 10_000_000;
const SECONDS_PER_DAY: i64 = 86_400;
/// Days from 1601-01-01 (the FILETIME epoch) to 1970-01-01.
const DAYS_1601_TO_1970: i64 = 134_774;

/// A UTC instant in 100 ns ticks since 1601-01-01, the unit `GetSystemTimeAsFileTime` returns.
#[derive(Clone, Copy, Debug, Default, PartialEq, Eq, PartialOrd, Ord, Hash)]
pub struct Timestamp(i64);

impl Timestamp {
    /// Wraps a FILETIME value read as a signed 64-bit number.
    pub const fn from_ticks(ticks: i64) -> Self {
        Timestamp(ticks)
    }

    /// The FILETIME value, for passing back to Windows.
    pub const fn ticks(self) -> i64 {
        self.0
    }

    /// `seconds` later, clamped at the largest representable instant.
    #[must_use]
    pub fn saturating_add_secs(self, seconds: u64) -> Self {
        let ticks = i64::try_from(seconds)
            .unwrap_or(i64::MAX)
            .saturating_mul(TICKS_PER_SECOND);
        Timestamp(self.0.saturating_add(ticks))
    }

    /// Whole seconds until `later`, rounded up so a countdown never reads 0 while time remains.
    pub fn secs_until(self, later: Timestamp) -> u64 {
        let ticks = later.0.saturating_sub(self.0);
        if ticks <= 0 {
            0
        } else {
            (ticks as u64).div_ceil(TICKS_PER_SECOND as u64)
        }
    }
}

/// A calendar date and time with no time zone attached.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct CivilDateTime {
    /// 1 to 9999.
    pub year: i32,
    /// 1 to 12.
    pub month: u8,
    /// 1 to the length of the month.
    pub day: u8,
    /// 0 to 23.
    pub hour: u8,
    /// 0 to 59.
    pub minute: u8,
    /// 0 to 59; leap seconds are not represented.
    pub second: u8,
    /// Sub-second part in 100 ns ticks.
    pub fraction: u32,
}

impl CivilDateTime {
    /// A whole-second date and time, or `None` when any part is out of range.
    pub fn new(year: i32, month: u8, day: u8, hour: u8, minute: u8, second: u8) -> Option<Self> {
        let valid = (1..=9999).contains(&year)
            && (1..=12).contains(&month)
            && day >= 1
            && day <= days_in_month(year, month)
            && hour < 24
            && minute < 60
            && second < 60;
        valid.then_some(CivilDateTime {
            year,
            month,
            day,
            hour,
            minute,
            second,
            fraction: 0,
        })
    }

    /// Interprets this date and time as UTC.
    pub fn to_timestamp(self) -> Timestamp {
        let days = days_from_civil(i64::from(self.year), u32::from(self.month), u32::from(self.day))
            + DAYS_1601_TO_1970;
        let seconds = days * SECONDS_PER_DAY
            + i64::from(self.hour) * 3600
            + i64::from(self.minute) * 60
            + i64::from(self.second);
        Timestamp(seconds * TICKS_PER_SECOND + i64::from(self.fraction))
    }

    /// The UTC calendar date and time of `at`.
    pub fn from_timestamp(at: Timestamp) -> Self {
        let seconds = at.0.div_euclid(TICKS_PER_SECOND);
        let fraction = at.0.rem_euclid(TICKS_PER_SECOND) as u32;
        let days = seconds.div_euclid(SECONDS_PER_DAY);
        let second_of_day = seconds.rem_euclid(SECONDS_PER_DAY);
        let (year, month, day) = civil_from_days(days - DAYS_1601_TO_1970);
        CivilDateTime {
            year: year as i32,
            month: month as u8,
            day: day as u8,
            hour: (second_of_day / 3600) as u8,
            minute: (second_of_day % 3600 / 60) as u8,
            second: (second_of_day % 60) as u8,
            fraction,
        }
    }
}

/// An instant plus the UTC offset it is expressed in, like .NET's `DateTimeOffset`.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct DateTimeOffset {
    /// The instant; comparisons and deadlines use only this.
    pub utc: Timestamp,
    /// Local time minus UTC, in minutes; only affects how the instant is written.
    pub offset_minutes: i16,
}

impl DateTimeOffset {
    /// `utc` expressed with `offset_minutes`.
    pub fn new(utc: Timestamp, offset_minutes: i16) -> Self {
        DateTimeOffset { utc, offset_minutes }
    }

    /// The calendar date and time in this value's own offset.
    pub fn local(&self) -> CivilDateTime {
        let offset = i64::from(self.offset_minutes) * 60 * TICKS_PER_SECOND;
        CivilDateTime::from_timestamp(Timestamp(self.utc.0.saturating_add(offset)))
    }

    /// `seconds` later in the same offset, clamped like [`Timestamp::saturating_add_secs`].
    #[must_use]
    pub fn saturating_add_secs(self, seconds: u64) -> Self {
        DateTimeOffset {
            utc: self.utc.saturating_add_secs(seconds),
            ..self
        }
    }

    /// .NET's `DateTime.MinValue` and similar placeholders that mean "not set".
    pub(crate) fn is_placeholder(&self) -> bool {
        self.local().year < 2
    }

    /// Formats like System.Text.Json: `2026-10-10T09:15:32.1234567-07:00`, trailing zeros trimmed.
    pub fn to_json_string(&self) -> String {
        let local = self.local();
        let mut text = format!(
            "{:04}-{:02}-{:02}T{:02}:{:02}:{:02}",
            local.year, local.month, local.day, local.hour, local.minute, local.second
        );
        if local.fraction != 0 {
            let digits = format!("{:07}", local.fraction);
            let _ = write!(text, ".{}", digits.trim_end_matches('0'));
        }
        let sign = if self.offset_minutes < 0 { '-' } else { '+' };
        let offset = self.offset_minutes.unsigned_abs();
        let _ = write!(text, "{sign}{:02}:{:02}", offset / 60, offset % 60);
        text
    }
}

/// Converts local calendar times to instants. Windows supplies the real time zone rules.
pub trait LocalZone {
    /// `None` when the local time does not exist, for example inside a daylight saving gap.
    fn resolve(&self, local: CivilDateTime) -> Option<DateTimeOffset>;
}

/// A zone with a constant offset, for tests and for UTC.
#[derive(Clone, Copy, Debug)]
pub struct FixedZone(pub i16);

impl LocalZone for FixedZone {
    fn resolve(&self, local: CivilDateTime) -> Option<DateTimeOffset> {
        let offset_ticks = i64::from(self.0) * 60 * TICKS_PER_SECOND;
        Some(DateTimeOffset::new(
            Timestamp(local.to_timestamp().0 - offset_ticks),
            self.0,
        ))
    }
}

/// What a time string from the user or a settings file resolved to.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum ParsedTime {
    /// A full instant with an explicit offset or `Z`.
    Absolute(DateTimeOffset),
    /// A date and time with no offset, to be read in the local time zone.
    Local(CivilDateTime),
    /// A time of day only; the next occurrence is meant.
    TimeOfDay {
        /// 0 to 23.
        hour: u8,
        /// 0 to 59.
        minute: u8,
        /// 0 to 59.
        second: u8,
    },
}

/// Parses ISO 8601 date-times (`2026-12-24T18:00`, `2026-12-24 18:00:00.5+01:00`), plain dates
/// and times of day (`17:30`, `5:30 pm`, `5pm`).
pub fn parse_time(text: &str) -> Option<ParsedTime> {
    let text = text.trim();
    if text.len() >= 10 && text.as_bytes()[4] == b'-' {
        parse_date_time(text)
    } else {
        parse_time_of_day(text).map(|(hour, minute, second)| ParsedTime::TimeOfDay { hour, minute, second })
    }
}

fn parse_date_time(text: &str) -> Option<ParsedTime> {
    if !text.is_char_boundary(10) {
        return None;
    }
    let (date, rest) = text.split_at(10);
    if date.as_bytes()[7] != b'-' {
        return None;
    }
    let year: i32 = digits(&date[0..4])?;
    let month: u8 = digits(&date[5..7])?;
    let day: u8 = digits(&date[8..10])?;
    if rest.is_empty() {
        return CivilDateTime::new(year, month, day, 0, 0, 0).map(ParsedTime::Local);
    }
    let rest = rest.strip_prefix(['T', 't', ' '])?;

    let (clock, zone) = match rest.find(['Z', 'z', '+', '-']) {
        Some(i) => rest.split_at(i),
        None => (rest, ""),
    };
    let (hms, fraction) = match clock.split_once(['.', ',']) {
        Some((hms, fraction)) => (hms, Some(fraction)),
        None => (clock, None),
    };
    let mut parts = hms.split(':');
    let hour: u8 = digits(parts.next()?)?;
    let minute: u8 = digits(parts.next()?)?;
    let second: u8 = parts.next().map_or(Some(0), digits)?;
    if parts.next().is_some() {
        return None;
    }
    let mut civil = CivilDateTime::new(year, month, day, hour, minute, second)?;
    if let Some(fraction) = fraction {
        if fraction.is_empty() || !fraction.bytes().all(|b| b.is_ascii_digit()) {
            return None;
        }
        // 100 ns resolution: .NET writes at most seven digits.
        let padded = format!("{fraction:0<7}");
        civil.fraction = padded[..7].parse().ok()?;
    }

    if zone.is_empty() {
        return Some(ParsedTime::Local(civil));
    }
    let offset_minutes = if zone.eq_ignore_ascii_case("z") {
        0
    } else {
        let sign = if zone.starts_with('-') { -1 } else { 1 };
        let body = &zone[1..];
        let (h, m) = match body.split_once(':') {
            Some(parts) => parts,
            // `len` counts bytes, so only all-digit text is known to split on a char boundary.
            None if body.len() == 4 && body.bytes().all(|b| b.is_ascii_digit()) => body.split_at(2),
            None => (body, "0"),
        };
        let h: i16 = digits(h)?;
        let m: i16 = digits(m)?;
        if h > 14 || m > 59 {
            return None;
        }
        sign * (h * 60 + m)
    };
    let utc = Timestamp(civil.to_timestamp().0 - i64::from(offset_minutes) * 60 * TICKS_PER_SECOND);
    Some(ParsedTime::Absolute(DateTimeOffset::new(utc, offset_minutes)))
}

fn parse_time_of_day(text: &str) -> Option<(u8, u8, u8)> {
    let lower = text.to_ascii_lowercase();
    let (clock, meridiem) = if let Some(clock) = lower.strip_suffix("am") {
        (clock.trim_end(), Some(false))
    } else if let Some(clock) = lower.strip_suffix("pm") {
        (clock.trim_end(), Some(true))
    } else {
        (lower.as_str(), None)
    };
    let mut parts = clock.split(':');
    let hour: u8 = digits(parts.next()?)?;
    let minute: u8 = match parts.next() {
        Some(m) if m.len() == 2 => digits(m)?,
        None if meridiem.is_some() => 0,
        _ => return None,
    };
    let second: u8 = match parts.next() {
        Some(s) if s.len() == 2 => digits(s)?,
        Some(_) => return None,
        None => 0,
    };
    if parts.next().is_some() || minute > 59 || second > 59 {
        return None;
    }
    let hour = match meridiem {
        Some(_) if !(1..=12).contains(&hour) => return None,
        Some(pm) => hour % 12 + if pm { 12 } else { 0 },
        None if hour > 23 => return None,
        None => hour,
    };
    Some((hour, minute, second))
}

fn digits<T: std::str::FromStr>(text: &str) -> Option<T> {
    if text.is_empty() || !text.bytes().all(|b| b.is_ascii_digit()) {
        return None;
    }
    text.parse().ok()
}

fn is_leap_year(year: i32) -> bool {
    (year % 4 == 0 && year % 100 != 0) || year % 400 == 0
}

fn days_in_month(year: i32, month: u8) -> u8 {
    match month {
        2 if is_leap_year(year) => 29,
        2 => 28,
        4 | 6 | 9 | 11 => 30,
        _ => 31,
    }
}

/// Days since 1970-01-01 for a proleptic Gregorian date (Howard Hinnant's algorithm).
fn days_from_civil(year: i64, month: u32, day: u32) -> i64 {
    let year = if month <= 2 { year - 1 } else { year };
    let era = if year >= 0 { year } else { year - 399 } / 400;
    let year_of_era = year - era * 400;
    let month_index = i64::from((month + 9) % 12);
    let day_of_year = (153 * month_index + 2) / 5 + i64::from(day) - 1;
    let day_of_era = year_of_era * 365 + year_of_era / 4 - year_of_era / 100 + day_of_year;
    era * 146_097 + day_of_era - 719_468
}

fn civil_from_days(days: i64) -> (i64, u32, u32) {
    let days = days + 719_468;
    let era = if days >= 0 { days } else { days - 146_096 } / 146_097;
    let day_of_era = days - era * 146_097;
    let year_of_era = (day_of_era - day_of_era / 1460 + day_of_era / 36_524 - day_of_era / 146_096) / 365;
    let day_of_year = day_of_era - (365 * year_of_era + year_of_era / 4 - year_of_era / 100);
    let month_index = (5 * day_of_year + 2) / 153;
    let day = (day_of_year - (153 * month_index + 2) / 5 + 1) as u32;
    let month = if month_index < 10 {
        month_index + 3
    } else {
        month_index - 9
    } as u32;
    let year = year_of_era + era * 400 + i64::from(month <= 2);
    (year, month, day)
}

#[cfg(test)]
mod tests {
    use super::*;

    fn absolute(text: &str) -> DateTimeOffset {
        match parse_time(text) {
            Some(ParsedTime::Absolute(at)) => at,
            other => panic!("{text}: {other:?}"),
        }
    }

    #[test]
    fn filetime_epoch_and_unix_epoch() {
        let epoch = CivilDateTime::new(1601, 1, 1, 0, 0, 0).unwrap();
        assert_eq!(epoch.to_timestamp().ticks(), 0);
        let unix = CivilDateTime::new(1970, 1, 1, 0, 0, 0).unwrap();
        assert_eq!(unix.to_timestamp().ticks(), 116_444_736_000_000_000);
    }

    #[test]
    fn civil_round_trip_across_eras() {
        for (y, m, d) in [
            (1, 1, 1),
            (1600, 2, 29),
            (1900, 3, 1),
            (2000, 2, 29),
            (2026, 10, 10),
            (9999, 12, 31),
        ] {
            let civil = CivilDateTime::new(y, m, d, 23, 59, 58).unwrap();
            assert_eq!(CivilDateTime::from_timestamp(civil.to_timestamp()), civil);
        }
    }

    #[test]
    fn rejects_impossible_dates() {
        assert!(CivilDateTime::new(2025, 2, 29, 0, 0, 0).is_none());
        assert!(CivilDateTime::new(2026, 4, 31, 0, 0, 0).is_none());
        assert!(CivilDateTime::new(2026, 1, 1, 24, 0, 0).is_none());
    }

    #[test]
    fn parses_dotnet_round_trip_format() {
        let at = absolute("2026-10-10T09:15:32.1234567+02:00");
        assert_eq!(at.offset_minutes, 120);
        assert_eq!(at.local().fraction, 1_234_567);
        assert_eq!(at.to_json_string(), "2026-10-10T09:15:32.1234567+02:00");
    }

    #[test]
    fn formats_like_system_text_json() {
        assert_eq!(
            absolute("2026-01-02T03:04:05-07:00").to_json_string(),
            "2026-01-02T03:04:05-07:00"
        );
        assert_eq!(
            absolute("2026-01-02T03:04:05.5Z").to_json_string(),
            "2026-01-02T03:04:05.5+00:00"
        );
        assert_eq!(
            absolute("0001-01-01T00:00:00+00:00").to_json_string(),
            "0001-01-01T00:00:00+00:00"
        );
    }

    #[test]
    fn offset_shifts_the_instant() {
        assert_eq!(
            absolute("2026-10-10T12:00:00+02:00").utc,
            absolute("2026-10-10T10:00:00Z").utc
        );
        assert_eq!(
            absolute("2026-10-10T12:00:00-0130").utc,
            absolute("2026-10-10T13:30:00Z").utc
        );
    }

    #[test]
    fn local_forms_have_no_offset() {
        assert!(matches!(parse_time("2026-12-24 18:00"), Some(ParsedTime::Local(c)) if c.hour == 18));
        assert!(matches!(parse_time("2026-12-24"), Some(ParsedTime::Local(c)) if c.day == 24 && c.hour == 0));
    }

    #[test]
    fn times_of_day() {
        assert_eq!(
            parse_time("17:30"),
            Some(ParsedTime::TimeOfDay {
                hour: 17,
                minute: 30,
                second: 0
            })
        );
        assert_eq!(
            parse_time("5:30 pm"),
            Some(ParsedTime::TimeOfDay {
                hour: 17,
                minute: 30,
                second: 0
            })
        );
        assert_eq!(
            parse_time("12am"),
            Some(ParsedTime::TimeOfDay {
                hour: 0,
                minute: 0,
                second: 0
            })
        );
        assert_eq!(
            parse_time("12:00:15 PM"),
            Some(ParsedTime::TimeOfDay {
                hour: 12,
                minute: 0,
                second: 15
            })
        );
    }

    #[test]
    fn rejects_garbage() {
        for text in [
            "",
            "abc",
            "25:00",
            "13pm",
            "17:3",
            "2026-13-01",
            "2026-10-10T10",
            "2026-10-10T10:00+99:00",
            "1:2:3:4",
        ] {
            assert_eq!(parse_time(text), None, "{text}");
        }
    }

    #[test]
    fn non_ascii_input_does_not_panic() {
        assert_eq!(parse_time("2026-10-1é"), None);
        assert_eq!(parse_time("１７:３０"), None);
        // Four bytes after the sign, but not four characters.
        assert_eq!(parse_time("2000-01-01T10:00+😀"), None);
        assert_eq!(parse_time("2000-01-01T10:00-a時"), None);
    }

    #[test]
    fn placeholder_dates_are_detected() {
        assert!(absolute("0001-01-01T00:00:00+00:00").is_placeholder());
        assert!(!absolute("2026-01-01T00:00:00+00:00").is_placeholder());
    }

    #[test]
    fn countdown_rounds_up() {
        let start = Timestamp::from_ticks(0);
        assert_eq!(start.secs_until(Timestamp::from_ticks(1)), 1);
        assert_eq!(start.secs_until(Timestamp::from_ticks(TICKS_PER_SECOND)), 1);
        assert_eq!(start.secs_until(Timestamp::from_ticks(TICKS_PER_SECOND + 1)), 2);
        assert_eq!(Timestamp::from_ticks(5).secs_until(start), 0);
    }
}
