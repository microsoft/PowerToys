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
    private static readonly CultureInfo UsCulture = CultureInfo.GetCultureInfo("en-US");
    private static readonly TimeZoneInfo Eastern = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
    private static readonly TimeZoneInfo Tokyo = TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time");
    private static readonly DateTimeOffset BaseNow = new(2026, 11, 22, 14, 0, 0, TimeSpan.FromHours(-5));

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("bananas")]
    [DataRow("not-a-date")]
    public void TryParse_InvalidInputs_ReturnsInvalidFormat(string input)
    {
        ExpirationTimeParseStatus status = ExpirationTimeParser.TryParse(
            input,
            BaseNow,
            UsCulture,
            Eastern,
            out DateTimeOffset result);

        Assert.AreEqual(ExpirationTimeParseStatus.InvalidFormat, status);
        Assert.AreEqual(default, result);
    }

    [TestMethod]
    [DataRow("2025-01-01 14:05")]
    [DataRow("2025-09-04T23:14:00")]
    [DataRow("2025-09-04T23:14:00Z")]
    [DataRow("09-04-25 23:14")]
    [DataRow("09/04/25 11:14 PM")]
    [DataRow("September 4 2025 11:14 PM")]
    [DataRow("4 September 2025 23:14:00")]
    [DataRow("2026-11-22 14:00")]
    [DataRow("2026-11-22T14:00:00Z")]
    [DataRow("2026-11-22T15:00:00+02:00")]
    public void TryParse_PastFullDatesAndIsoStrings_ReturnsInThePast(string input)
    {
        ExpirationTimeParseStatus status = ExpirationTimeParser.TryParse(
            input,
            BaseNow,
            UsCulture,
            Eastern,
            out DateTimeOffset result);

        Assert.AreEqual(ExpirationTimeParseStatus.InThePast, status);
        Assert.AreEqual(default, result);
    }

    [TestMethod]
    public void TryParse_FutureDatesAndOffsets_ReturnsSuccess()
    {
        AssertSuccess("2026-11-22 15:00", BaseNow, UsCulture, Eastern, new DateTimeOffset(2026, 11, 22, 15, 0, 0, TimeSpan.FromHours(-5)));
        AssertSuccess("2026-11-22T15:00:00", new DateTimeOffset(2026, 11, 22, 14, 0, 0, TimeSpan.FromHours(9)), UsCulture, Tokyo, new DateTimeOffset(2026, 11, 22, 15, 0, 0, TimeSpan.FromHours(9)));
        AssertSuccess("2026-11-22T20:30:00Z", BaseNow, UsCulture, Eastern, new DateTimeOffset(2026, 11, 22, 20, 30, 0, TimeSpan.Zero));
        AssertSuccess("2026-11-22T22:30:00+02:00", BaseNow, UsCulture, Eastern, new DateTimeOffset(2026, 11, 22, 22, 30, 0, TimeSpan.FromHours(2)));
    }

    [TestMethod]
    [DataRow("17:00", 17, 0)]
    [DataRow("23:14:00", 23, 14)]
    [DataRow("11:14 PM", 23, 14)]
    public void TryParse_TimeOnlyFutureToday_UsesToday(string input, int hour, int minute)
    {
        DateTimeOffset expected = new(2026, 11, 22, hour, minute, 0, TimeSpan.FromHours(-5));
        AssertSuccess(input, BaseNow, UsCulture, Eastern, expected);
    }

    [TestMethod]
    [DataRow("00:00:00", 0, 0)]
    [DataRow("08:00", 8, 0)]
    [DataRow("14:00:00", 14, 0)]
    [DataRow("12:00 AM", 0, 0)]
    public void TryParse_TimeOnlyPastOrNow_RollsToTomorrow(string input, int hour, int minute)
    {
        DateTimeOffset expected = new(2026, 11, 23, hour, minute, 0, TimeSpan.FromHours(-5));
        AssertSuccess(input, BaseNow, UsCulture, Eastern, expected);
    }

    [TestMethod]
    public void TryParse_TimeOnlyAcrossOvernightDstChange_UsesNextDayOffset()
    {
        DateTimeOffset beforeSpringForward = new(2026, 3, 7, 23, 0, 0, TimeSpan.FromHours(-5));
        AssertSuccess("08:00", beforeSpringForward, UsCulture, Eastern, new DateTimeOffset(2026, 3, 8, 8, 0, 0, TimeSpan.FromHours(-4)));

        DateTimeOffset beforeFallBack = new(2026, 10, 31, 23, 0, 0, TimeSpan.FromHours(-4));
        AssertSuccess("08:00", beforeFallBack, UsCulture, Eastern, new DateTimeOffset(2026, 11, 1, 8, 0, 0, TimeSpan.FromHours(-5)));
    }

    [TestMethod]
    public void TryParse_BoundaryDates_DoNotThrow()
    {
        Assert.AreEqual(
            ExpirationTimeParseStatus.InThePast,
            ExpirationTimeParser.TryParse("0001-01-01", BaseNow, UsCulture, Eastern, out _));
        Assert.AreEqual(
            ExpirationTimeParseStatus.InThePast,
            ExpirationTimeParser.TryParse("0001-01-01", BaseNow, UsCulture, Tokyo, out _));
        Assert.AreEqual(
            ExpirationTimeParseStatus.InvalidFormat,
            ExpirationTimeParser.TryParse("9999-12-31 23:59:59", BaseNow, UsCulture, Eastern, out _));
    }

    [TestMethod]
    public void TryParse_NonUsCultures_ForwardsCulture()
    {
        DateTimeOffset utcNow = new(2026, 11, 22, 14, 0, 0, TimeSpan.Zero);

        Assert.AreEqual(
            ExpirationTimeParseStatus.InThePast,
            ExpirationTimeParser.TryParse("17.08.2025 14:05", utcNow, CultureInfo.GetCultureInfo("de-DE"), TimeZoneInfo.Utc, out _));

        CultureInfo finnish = CultureInfo.GetCultureInfo("fi-FI");
        AssertSuccess("17.00", utcNow, finnish, TimeZoneInfo.Utc, new DateTimeOffset(2026, 11, 22, 17, 0, 0, TimeSpan.Zero));
        AssertSuccess("08.30", utcNow, finnish, TimeZoneInfo.Utc, new DateTimeOffset(2026, 11, 23, 8, 30, 0, TimeSpan.Zero));
    }

    private static void AssertSuccess(
        string input,
        DateTimeOffset now,
        CultureInfo culture,
        TimeZoneInfo timeZone,
        DateTimeOffset expected)
    {
        ExpirationTimeParseStatus status = ExpirationTimeParser.TryParse(
            input,
            now,
            culture,
            timeZone,
            out DateTimeOffset result);

        Assert.AreEqual(ExpirationTimeParseStatus.Success, status);
        Assert.AreEqual(expected, result);
        Assert.AreEqual(expected.Offset, result.Offset);
    }
}
