//! Wall-clock time and the local time zone, from Windows.

use std::{mem, ptr};

use awake_core::time::{CivilDateTime, DateTimeOffset, LocalZone, TICKS_PER_SECOND, Timestamp};
use windows_sys::Win32::Foundation::{FILETIME, SYSTEMTIME};
use windows_sys::Win32::Globalization::{GetDateFormatEx, GetTimeFormatEx, LOCALE_USER_DEFAULT};
use windows_sys::Win32::System::Ole::VarDateFromStr;
use windows_sys::Win32::System::SystemInformation::GetSystemTimePreciseAsFileTime;
use windows_sys::Win32::System::Time::{
    FileTimeToSystemTime, SystemTimeToFileTime, SystemTimeToTzSpecificLocalTimeEx,
    TzSpecificLocalTimeToSystemTimeEx,
};
use windows_sys::Win32::System::Variant::VariantTimeToSystemTime;

use super::wide::{from_buffer, wide};

#[derive(Clone, Copy, Debug, Default)]
pub struct SystemClock;

#[allow(
    clippy::unused_self,
    reason = "callers read the time through a clock value, though the system clock has no state"
)]
impl SystemClock {
    /// The time with the local offset. Working out the offset takes three time-zone
    /// conversions, so code that only compares or subtracts times uses `now_utc` instead.
    pub fn now(self) -> DateTimeOffset {
        let utc = self.now_utc();
        DateTimeOffset::new(utc, self.offset_minutes_at(utc))
    }

    /// The time in UTC: one cheap call, for countdowns, deadlines and other comparisons.
    pub fn now_utc(self) -> Timestamp {
        let mut ft = FILETIME {
            dwLowDateTime: 0,
            dwHighDateTime: 0,
        };
        // SAFETY: `ft` is valid for writes.
        unsafe { GetSystemTimePreciseAsFileTime(&mut ft) };
        Timestamp::from_ticks(filetime_ticks(ft))
    }

    /// The local UTC offset in effect at `utc`, honoring daylight saving rules.
    pub fn offset_minutes_at(self, utc: Timestamp) -> i16 {
        let Some(utc_st) = to_system_time(utc) else {
            return 0;
        };
        // SAFETY: SYSTEMTIME is plain data.
        let mut local_st: SYSTEMTIME = unsafe { mem::zeroed() };
        // SAFETY: null selects the current time zone; both structs are valid.
        if unsafe { SystemTimeToTzSpecificLocalTimeEx(ptr::null(), &utc_st, &mut local_st) } == 0 {
            return 0;
        }
        let local = from_system_time(&local_st).to_timestamp();
        ((local.ticks() - utc.ticks()) / (60 * TICKS_PER_SECOND)) as i16
    }

    /// `Oct 10, 5:30 PM` in the user's regional format (`MMM d, h:mm tt`).
    pub fn format_expiry(self, at: &DateTimeOffset) -> String {
        let civil = at.local();
        let st = SYSTEMTIME {
            wYear: civil.year.clamp(1601, 30827) as u16,
            wMonth: u16::from(civil.month),
            wDayOfWeek: 0,
            wDay: u16::from(civil.day),
            wHour: u16::from(civil.hour),
            wMinute: u16::from(civil.minute),
            wSecond: u16::from(civil.second),
            wMilliseconds: 0,
        };
        let date_pattern = wide("MMM d");
        let time_pattern = wide("h:mm tt");
        let mut date = [0u16; 64];
        let mut time = [0u16; 64];
        // SAFETY: null locale means the user's default; buffers and lengths match.
        let (date_len, time_len) = unsafe {
            (
                GetDateFormatEx(
                    ptr::null(),
                    0,
                    &st,
                    date_pattern.as_ptr(),
                    date.as_mut_ptr(),
                    date.len() as i32,
                    ptr::null(),
                ),
                GetTimeFormatEx(
                    ptr::null(),
                    0,
                    &st,
                    time_pattern.as_ptr(),
                    time.as_mut_ptr(),
                    time.len() as i32,
                ),
            )
        };
        if date_len <= 0 || time_len <= 0 {
            return format!(
                "{:04}-{:02}-{:02} {:02}:{:02}",
                civil.year, civil.month, civil.day, civil.hour, civil.minute
            );
        }
        format!("{}, {}", from_buffer(&date), from_buffer(&time))
    }

    /// Parses a date and time written in the user's regional format, as `DateTimeOffset.Parse`
    /// with the current culture did for `--expire-at`.
    pub fn parse_regional(self, text: &str) -> Option<CivilDateTime> {
        let text = wide(text);
        let mut date = 0f64;
        // SAFETY: `text` is NUL-terminated; `date` is valid for writes.
        if unsafe { VarDateFromStr(text.as_ptr(), LOCALE_USER_DEFAULT, 0, &mut date) } < 0 {
            return None;
        }
        // SAFETY: SYSTEMTIME is plain data and valid for writes.
        let mut st: SYSTEMTIME = unsafe { mem::zeroed() };
        // SAFETY: as above.
        if unsafe { VariantTimeToSystemTime(date, &mut st) } == 0 {
            return None;
        }
        Some(from_system_time(&st))
    }
}

impl LocalZone for SystemClock {
    fn resolve(&self, local: CivilDateTime) -> Option<DateTimeOffset> {
        let local_ticks = local.to_timestamp().ticks();
        let local_st = to_system_time(local.to_timestamp())?;
        // SAFETY: SYSTEMTIME is plain data.
        let mut utc_st: SYSTEMTIME = unsafe { mem::zeroed() };
        // SAFETY: null selects the current time zone; both structs are valid.
        if unsafe { TzSpecificLocalTimeToSystemTimeEx(ptr::null(), &local_st, &mut utc_st) } == 0 {
            return None;
        }
        let whole_local = from_system_time(&local_st).to_timestamp().ticks();
        let whole_utc = from_system_time(&utc_st).to_timestamp().ticks();
        let offset = ((whole_local - whole_utc) / (60 * TICKS_PER_SECOND)) as i16;
        let utc = Timestamp::from_ticks(local_ticks - i64::from(offset) * 60 * TICKS_PER_SECOND);
        Some(DateTimeOffset::new(utc, offset))
    }
}

fn filetime_ticks(ft: FILETIME) -> i64 {
    ((u64::from(ft.dwHighDateTime) << 32) | u64::from(ft.dwLowDateTime)) as i64
}

fn to_system_time(at: Timestamp) -> Option<SYSTEMTIME> {
    let ticks = u64::try_from(at.ticks()).ok()?;
    let ft = FILETIME {
        dwLowDateTime: ticks as u32,
        dwHighDateTime: (ticks >> 32) as u32,
    };
    // SAFETY: SYSTEMTIME is plain data.
    let mut st: SYSTEMTIME = unsafe { mem::zeroed() };
    // SAFETY: both pointers are valid.
    (unsafe { FileTimeToSystemTime(&ft, &mut st) } != 0).then_some(st)
}

fn from_system_time(st: &SYSTEMTIME) -> CivilDateTime {
    let mut ft = FILETIME {
        dwLowDateTime: 0,
        dwHighDateTime: 0,
    };
    // SAFETY: both pointers are valid.
    if unsafe { SystemTimeToFileTime(st, &mut ft) } != 0 {
        return CivilDateTime::from_timestamp(Timestamp::from_ticks(filetime_ticks(ft)));
    }
    CivilDateTime::from_timestamp(Timestamp::default())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn local_times_round_trip_through_the_local_zone() {
        // Noon, because no zone changes its clocks then: around a transition a local time can
        // be missing or repeated, and would not round-trip.
        let clock = SystemClock;
        let today = clock.now().local();
        let noon = CivilDateTime::new(today.year, today.month, today.day, 12, 0, 0).unwrap();
        let resolved = clock.resolve(noon).unwrap();
        assert_eq!(resolved.local(), noon);
        assert_eq!(resolved.offset_minutes, clock.offset_minutes_at(resolved.utc));
    }

    #[test]
    fn now_and_now_utc_agree() {
        let clock = SystemClock;
        let before = clock.now_utc();
        let now = clock.now();
        let after = clock.now_utc();
        assert!(before <= now.utc && now.utc <= after);
        assert_eq!(now.offset_minutes, clock.offset_minutes_at(now.utc));
    }

    #[test]
    fn expiry_text_is_short_and_readable() {
        let clock = SystemClock;
        let text = clock.format_expiry(&clock.now());
        assert!(text.contains(", "), "{text}");
        assert!(text.len() < 40, "{text}");
    }

    #[test]
    fn regional_parse_accepts_iso_dates() {
        let parsed = SystemClock.parse_regional("2026-12-24 18:30").unwrap();
        assert_eq!(
            (parsed.year, parsed.month, parsed.day, parsed.hour, parsed.minute),
            (2026, 12, 24, 18, 30)
        );
        assert!(SystemClock.parse_regional("not a date").is_none());
    }
}
