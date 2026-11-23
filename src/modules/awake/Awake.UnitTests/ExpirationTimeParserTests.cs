// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using Awake.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Awake.UnitTests;

[TestClass]
public class ExpirationTimeParserTests
{
    [TestMethod]
    [DataRow((string)null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("bananas")]
    [DataRow("not-a-date")]
    public void TryParse_WhenInputIsNullEmptyWhitespaceOrInvalid_ReturnsInvalidFormat(string input)
    {
        DateTimeOffset now = new(2026, 11, 22, 14, 0, 0, TimeSpan.FromHours(-5));

        ExpirationTimeParseStatus status = ExpirationTimeParser.TryParse(
            input,
            now,
            CultureInfo.GetCultureInfo("en-US"),
            TimeZoneInfo.Utc,
            out DateTimeOffset result);

        Assert.AreEqual(ExpirationTimeParseStatus.InvalidFormat, status);
        Assert.AreEqual(default(DateTimeOffset), result);
    }

    [TestMethod]
    [DataRow("2026-11-22T15:30:00Z", 2026, 11, 22, 15, 30, 0, 0)]
    [DataRow("2026-11-22T17:30:00+02:00", 2026, 11, 22, 17, 30, 0, 2)]
    [DataRow("  2026-11-22T15:30:00Z  ", 2026, 11, 22, 15, 30, 0, 0)]
    public void TryParse_WhenOffsetQualifiedTimestampIsInFuture_ReturnsSuccessWithPreservedOffset(
        string input, int year, int month, int day, int hour, int minute, int second, int offsetHours)
    {
        DateTimeOffset now = new(2026, 11, 22, 14, 0, 0, TimeSpan.Zero);

        ExpirationTimeParseStatus status = ExpirationTimeParser.TryParse(
            input,
            now,
            CultureInfo.GetCultureInfo("en-US"),
            TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"),
            out DateTimeOffset result);

        DateTimeOffset expected = new(year, month, day, hour, minute, second, TimeSpan.FromHours(offsetHours));
        Assert.AreEqual(ExpirationTimeParseStatus.Success, status);
        Assert.AreEqual(expected, result);
        Assert.AreEqual(expected.Offset, result.Offset);
    }

    [TestMethod]
    [DataRow("2025-09-04T23:14:00Z")]
    [DataRow("2026-11-22T14:00:00Z")]
    [DataRow("2026-11-22T15:00:00+02:00")]
    public void TryParse_WhenOffsetQualifiedTimestampIsPastOrExactNow_ReturnsInThePast(string input)
    {
        DateTimeOffset now = new(2026, 11, 22, 14, 0, 0, TimeSpan.Zero);

        ExpirationTimeParseStatus status = ExpirationTimeParser.TryParse(
            input,
            now,
            CultureInfo.GetCultureInfo("en-US"),
            TimeZoneInfo.Utc,
            out _);

        Assert.AreEqual(ExpirationTimeParseStatus.InThePast, status);
    }

    [TestMethod]
    [DataRow("00:30Z")]
    [DataRow("17:00+02:00")]
    [DataRow("01:00-05:00")]
    public void TryParse_WhenOffsetQualifiedTimeOnlyIsPast_DoesNotRollToTomorrow(string input)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow.AddDays(2);

        ExpirationTimeParseStatus status = ExpirationTimeParser.TryParse(
            input,
            now,
            CultureInfo.GetCultureInfo("en-US"),
            TimeZoneInfo.Utc,
            out _);

        Assert.AreEqual(ExpirationTimeParseStatus.InThePast, status);
    }

    [TestMethod]
    [DataRow("2025-01-01 14:05")]
    [DataRow("2025-09-04T23:14:00")]
    [DataRow("09-04-25 23:14")]
    [DataRow("09/04/25 11:14 PM")]
    [DataRow("September 4 2025 11:14 PM")]
    [DataRow("4 September 2025 23:14:00")]
    [DataRow("2026-11-22")]
    [DataRow("2026-11-22 14:00")]
    public void TryParse_WhenExplicitLocalDateIsPastOrExactNow_ReturnsInThePast(string input)
    {
        TimeZoneInfo eastern = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        DateTimeOffset now = new(2026, 11, 22, 14, 0, 0, TimeSpan.FromHours(-5));

        ExpirationTimeParseStatus status = ExpirationTimeParser.TryParse(
            input,
            now,
            CultureInfo.GetCultureInfo("en-US"),
            eastern,
            out _);

        Assert.AreEqual(ExpirationTimeParseStatus.InThePast, status);
    }

    [TestMethod]
    [DataRow("2026-11-22 15:00", "Eastern Standard Time", 2026, 11, 22, 15, 0, -5)]
    [DataRow("  2026-11-22 15:00  ", "Eastern Standard Time", 2026, 11, 22, 15, 0, -5)]
    [DataRow("2026-11-22T15:00:00", "Tokyo Standard Time", 2026, 11, 22, 15, 0, 9)]
    [DataRow("2026-11-23", "Eastern Standard Time", 2026, 11, 23, 0, 0, -5)]
    public void TryParse_WhenExplicitLocalDateIsInFuture_ResolvesInSuppliedTimeZone(
        string input, string timeZoneId, int year, int month, int day, int hour, int minute, int offsetHours)
    {
        TimeZoneInfo tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        DateTimeOffset now = new(2026, 11, 22, 14, 0, 0, TimeSpan.FromHours(offsetHours));

        ExpirationTimeParseStatus status = ExpirationTimeParser.TryParse(
            input,
            now,
            CultureInfo.GetCultureInfo("en-US"),
            tz,
            out DateTimeOffset result);

        DateTimeOffset expected = new(year, month, day, hour, minute, 0, TimeSpan.FromHours(offsetHours));
        Assert.AreEqual(ExpirationTimeParseStatus.Success, status);
        Assert.AreEqual(expected, result);
        Assert.AreEqual(expected.Offset, result.Offset);
    }

    [TestMethod]
    [DataRow("0001-01-01", "UTC")]
    [DataRow("0001-01-01", "Eastern Standard Time")]
    [DataRow("0001-01-01", "W. Europe Standard Time")]
    [DataRow("0001-01-01", "Tokyo Standard Time")]
    [DataRow("0001-01-01 00:00:00", "Tokyo Standard Time")]
    [DataRow("0001-01-01 14:05", "Eastern Standard Time")]
    public void TryParse_WhenLiteralYearOneDateAcrossTimeZones_ReturnsInThePast(string input, string timeZoneId)
    {
        TimeZoneInfo tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        DateTimeOffset now = new(2026, 11, 22, 14, 0, 0, TimeSpan.Zero);

        ExpirationTimeParseStatus status = ExpirationTimeParser.TryParse(
            input,
            now,
            CultureInfo.GetCultureInfo("en-US"),
            tz,
            out _);

        Assert.AreEqual(ExpirationTimeParseStatus.InThePast, status);
    }

    [TestMethod]
    public void TryParse_WhenMaxDateOverflowsUtcRange_ReturnsInvalidFormat()
    {
        TimeZoneInfo eastern = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        DateTimeOffset now = new(2026, 11, 22, 14, 0, 0, TimeSpan.FromHours(-5));

        ExpirationTimeParseStatus status = ExpirationTimeParser.TryParse(
            "9999-12-31 23:59:59",
            now,
            CultureInfo.GetCultureInfo("en-US"),
            eastern,
            out DateTimeOffset result);

        Assert.AreEqual(ExpirationTimeParseStatus.InvalidFormat, status);
        Assert.AreEqual(default(DateTimeOffset), result);
    }

    [TestMethod]
    [DataRow("23:14:00", 23, 14, 0, 0)]
    [DataRow("11:14 PM", 23, 14, 0, 0)]
    [DataRow("11:14PM", 23, 14, 0, 0)]
    [DataRow("11:14:30 PM", 23, 14, 30, 0)]
    [DataRow("17:00:00.500", 17, 0, 0, 500)]
    [DataRow("  17:00  ", 17, 0, 0, 0)]
    public void TryParse_WhenLocalTimeOnlyIsStillInFutureToday_UsesTodayInSuppliedTimeZone(
        string input, int hour, int minute, int second, int millisecond)
    {
        TimeZoneInfo eastern = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        DateTimeOffset nowUtc = new(2026, 11, 22, 19, 0, 0, TimeSpan.Zero);

        ExpirationTimeParseStatus status = ExpirationTimeParser.TryParse(
            input,
            nowUtc,
            CultureInfo.GetCultureInfo("en-US"),
            eastern,
            out DateTimeOffset result);

        DateTimeOffset expected = new(2026, 11, 22, hour, minute, second, millisecond, TimeSpan.FromHours(-5));
        Assert.AreEqual(ExpirationTimeParseStatus.Success, status);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("00:00:00", 0, 0, 0)]
    [DataRow("08:00", 8, 0, 0)]
    [DataRow("9:05:00", 9, 5, 0)]
    [DataRow("14:00:00", 14, 0, 0)]
    [DataRow("12:00 AM", 0, 0, 0)]
    [DataRow("12:30 AM", 0, 30, 0)]
    [DataRow("12:00 PM", 12, 0, 0)]
    public void TryParse_WhenLocalTimeOnlyIsPastOrExactNow_RollsForwardToTomorrow(
        string input, int hour, int minute, int second)
    {
        TimeZoneInfo eastern = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        DateTimeOffset now = new(2026, 11, 22, 14, 0, 0, TimeSpan.FromHours(-5));

        ExpirationTimeParseStatus status = ExpirationTimeParser.TryParse(
            input,
            now,
            CultureInfo.GetCultureInfo("en-US"),
            eastern,
            out DateTimeOffset result);

        DateTimeOffset expected = new(2026, 11, 23, hour, minute, second, TimeSpan.FromHours(-5));
        Assert.AreEqual(ExpirationTimeParseStatus.Success, status);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void TryParse_WhenNoonIsStillFutureToday_ResolvesToTodayNoon()
    {
        TimeZoneInfo eastern = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        DateTimeOffset now = new(2026, 11, 22, 11, 0, 0, TimeSpan.FromHours(-5));

        ExpirationTimeParseStatus status = ExpirationTimeParser.TryParse(
            "12:00 PM",
            now,
            CultureInfo.GetCultureInfo("en-US"),
            eastern,
            out DateTimeOffset result);

        Assert.AreEqual(ExpirationTimeParseStatus.Success, status);
        Assert.AreEqual(new DateTimeOffset(2026, 11, 22, 12, 0, 0, TimeSpan.FromHours(-5)), result);
    }

    [TestMethod]
    [DataRow(2026, 12, 31, 23, 30, "00:30", 2027, 1, 1, 0, 30)]
    [DataRow(2024, 2, 28, 23, 30, "00:30", 2024, 2, 29, 0, 30)]
    [DataRow(2025, 2, 28, 23, 30, "00:30", 2025, 3, 1, 0, 30)]
    public void TryParse_WhenTimeOnlyRollsAcrossMonthLeapDayOrYearEnd_LandsOnNextCalendarDay(
        int nowYear,
        int nowMonth,
        int nowDay,
        int nowHour,
        int nowMinute,
        string input,
        int expYear,
        int expMonth,
        int expDay,
        int expHour,
        int expMinute)
    {
        DateTimeOffset now = new(nowYear, nowMonth, nowDay, nowHour, nowMinute, 0, TimeSpan.Zero);

        ExpirationTimeParseStatus status = ExpirationTimeParser.TryParse(
            input,
            now,
            CultureInfo.GetCultureInfo("en-US"),
            TimeZoneInfo.Utc,
            out DateTimeOffset result);

        Assert.AreEqual(ExpirationTimeParseStatus.Success, status);
        Assert.AreEqual(new DateTimeOffset(expYear, expMonth, expDay, expHour, expMinute, 0, TimeSpan.Zero), result);
    }

    [TestMethod]
    public void TryParse_WhenTimeOnlyHitsSpringForwardGapBeforeTransition_ShiftsForwardToday()
    {
        TimeZoneInfo eastern = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        DateTimeOffset now = new(2026, 3, 8, 1, 30, 0, TimeSpan.FromHours(-5));

        ExpirationTimeParseStatus status = ExpirationTimeParser.TryParse(
            "02:30",
            now,
            CultureInfo.GetCultureInfo("en-US"),
            eastern,
            out DateTimeOffset result);

        DateTimeOffset expected = new(2026, 3, 8, 3, 30, 0, TimeSpan.FromHours(-4));
        Assert.AreEqual(ExpirationTimeParseStatus.Success, status);
        Assert.AreEqual(expected, result);
        Assert.AreEqual(expected.Offset, result.Offset);
    }

    [TestMethod]
    public void TryParse_WhenTimeOnlyHitsSpringForwardGapAfterTransition_RollsToTomorrow()
    {
        TimeZoneInfo eastern = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        DateTimeOffset now = new(2026, 3, 8, 3, 15, 0, TimeSpan.FromHours(-4));

        ExpirationTimeParseStatus status = ExpirationTimeParser.TryParse(
            "02:30",
            now,
            CultureInfo.GetCultureInfo("en-US"),
            eastern,
            out DateTimeOffset result);

        DateTimeOffset expected = new(2026, 3, 9, 2, 30, 0, TimeSpan.FromHours(-4));
        Assert.AreEqual(ExpirationTimeParseStatus.Success, status);
        Assert.AreEqual(expected, result);
        Assert.AreEqual(expected.Offset, result.Offset);
    }

    [TestMethod]
    public void TryParse_WhenTimeOnlyRollsToTomorrowSpringForwardGap_ShiftsTomorrowForward()
    {
        TimeZoneInfo eastern = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        DateTimeOffset now = new(2026, 3, 7, 23, 0, 0, TimeSpan.FromHours(-5));

        ExpirationTimeParseStatus status = ExpirationTimeParser.TryParse(
            "02:30",
            now,
            CultureInfo.GetCultureInfo("en-US"),
            eastern,
            out DateTimeOffset result);

        DateTimeOffset expected = new(2026, 3, 8, 3, 30, 0, TimeSpan.FromHours(-4));
        Assert.AreEqual(ExpirationTimeParseStatus.Success, status);
        Assert.AreEqual(expected, result);
        Assert.AreEqual(expected.Offset, result.Offset);
    }

    [TestMethod]
    public void TryParse_WhenTimeOnlyHitsThirtyMinuteSpringForwardGap_ShiftsByThirtyMinutes()
    {
        TimeZoneInfo lordHowe = TimeZoneInfo.FindSystemTimeZoneById("Lord Howe Standard Time");
        DateTimeOffset now = new(2026, 10, 4, 1, 30, 0, TimeSpan.FromMinutes(630));

        ExpirationTimeParseStatus status = ExpirationTimeParser.TryParse(
            "02:15",
            now,
            CultureInfo.GetCultureInfo("en-AU"),
            lordHowe,
            out DateTimeOffset result);

        DateTimeOffset expected = new(2026, 10, 4, 2, 45, 0, TimeSpan.FromHours(11));
        Assert.AreEqual(ExpirationTimeParseStatus.Success, status);
        Assert.AreEqual(expected, result);
        Assert.AreEqual(expected.Offset, result.Offset);
    }

    [TestMethod]
    public void TryParse_WhenExplicitLocalDateHitsSpringForwardGap_ShiftsConsistently()
    {
        TimeZoneInfo eastern = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        DateTimeOffset now = new(2026, 3, 8, 1, 30, 0, TimeSpan.FromHours(-5));

        ExpirationTimeParseStatus status = ExpirationTimeParser.TryParse(
            "2026-03-08 02:30",
            now,
            CultureInfo.GetCultureInfo("en-US"),
            eastern,
            out DateTimeOffset result);

        DateTimeOffset expected = new(2026, 3, 8, 3, 30, 0, TimeSpan.FromHours(-4));
        Assert.AreEqual(ExpirationTimeParseStatus.Success, status);
        Assert.AreEqual(expected, result);
        Assert.AreEqual(expected.Offset, result.Offset);
    }
}
