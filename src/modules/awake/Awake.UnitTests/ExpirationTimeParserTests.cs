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
}
