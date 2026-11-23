// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Awake.UnitTests;

[TestClass]
public class ProgramCliParsingTests
{
    [TestMethod]
    public void BuildRootCommand_WhenExpireAtIsPastExplicitDate_ReportsFutureError()
    {
        TimeZoneInfo eastern = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        string[][] cases =
        [
            ["--expire-at", "2025-01-01 14:05"],
            ["--expire-at=2025-01-01 14:05"],
            ["-e", "2025-01-01 14:05"],
        ];

        foreach (string[] args in cases)
        {
            FakeTimeProvider clock = new(new DateTimeOffset(2026, 11, 22, 19, 0, 0, TimeSpan.Zero), eastern);
            RootCommand rootCommand = Program.BuildRootCommand(clock, CultureInfo.GetCultureInfo("en-US"));

            ParseResult parseResult = rootCommand.Parse(args);

            Assert.AreEqual(1, parseResult.Errors.Count);
            Assert.AreEqual("Date and time value in --expire-at must be in the future. Value used: 2025-01-01 14:05.", parseResult.Errors[0].Message);
        }
    }

    [TestMethod]
    public void BuildRootCommand_WhenExpireAtIsUnparseable_PreservesExistingErrorText()
    {
        string[][] cases =
        [
            ["--expire-at", "not-a-date"],
            ["-e=not-a-date"],
        ];

        foreach (string[] args in cases)
        {
            FakeTimeProvider clock = new(new DateTimeOffset(2026, 11, 22, 19, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc);
            RootCommand rootCommand = Program.BuildRootCommand(clock, CultureInfo.GetCultureInfo("en-US"));

            ParseResult parseResult = rootCommand.Parse(args);

            Assert.AreEqual(1, parseResult.Errors.Count);
            Assert.IsTrue(
                parseResult.Errors[0].Message.Contains("Date and time value in --expire-at could not be parsed correctly", StringComparison.Ordinal),
                $"Unexpected error message: {parseResult.Errors[0].Message}");
        }
    }

    [TestMethod]
    public void BuildRootCommand_WhenExpireAtIsPastTimeOnly_RollsForwardToTomorrowWithNoErrors()
    {
        TimeZoneInfo eastern = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        (string[] Args, DateTimeOffset Expected)[] cases =
        [
            (["--expire-at=00:00:00"], new DateTimeOffset(2026, 11, 23, 0, 0, 0, TimeSpan.FromHours(-5))),
            (["-e", "08:00"], new DateTimeOffset(2026, 11, 23, 8, 0, 0, TimeSpan.FromHours(-5))),
        ];

        foreach ((string[] args, DateTimeOffset expected) in cases)
        {
            FakeTimeProvider clock = new(new DateTimeOffset(2026, 11, 22, 19, 0, 0, TimeSpan.Zero), eastern);
            RootCommand rootCommand = Program.BuildRootCommand(clock, CultureInfo.GetCultureInfo("en-US"));

            ParseResult parseResult = rootCommand.Parse(args);

            Assert.AreEqual(0, parseResult.Errors.Count);
            DateTimeOffset? result = parseResult.GetValue<DateTimeOffset?>("--expire-at");
            Assert.IsNotNull(result);
            Assert.AreEqual(expected, result.Value);
            Assert.AreEqual(expected.Offset, result.Value.Offset);
        }
    }

    [TestMethod]
    public void BuildRootCommand_WhenExpireAtIsFutureIso8601_SucceedsWithExpectedTimestamp()
    {
        TimeZoneInfo eastern = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        FakeTimeProvider clock = new(new DateTimeOffset(2026, 11, 22, 19, 0, 0, TimeSpan.Zero), eastern);
        RootCommand rootCommand = Program.BuildRootCommand(clock, CultureInfo.GetCultureInfo("en-US"));

        ParseResult parseResult = rootCommand.Parse(["--expire-at", "2026-11-22T20:00:00Z"]);

        Assert.AreEqual(0, parseResult.Errors.Count);
        DateTimeOffset? result = parseResult.GetValue<DateTimeOffset?>("--expire-at");
        DateTimeOffset expected = new(2026, 11, 22, 20, 0, 0, TimeSpan.Zero);
        Assert.IsNotNull(result);
        Assert.AreEqual(expected, result.Value);
        Assert.AreEqual(expected.Offset, result.Value.Offset);
    }

    [TestMethod]
    public void BuildRootCommand_WhenExpireAtIsPastWithConfigOrPid_ReportsFutureError()
    {
        string[][] cases =
        [
            ["--use-pt-config", "--expire-at", "2020-01-01"],
            ["--pid", "1234", "--expire-at", "2020-01-01"],
        ];

        foreach (string[] args in cases)
        {
            FakeTimeProvider clock = new(new DateTimeOffset(2026, 11, 22, 19, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc);
            RootCommand rootCommand = Program.BuildRootCommand(clock, CultureInfo.GetCultureInfo("en-US"));

            ParseResult parseResult = rootCommand.Parse(args);

            Assert.AreEqual(1, parseResult.Errors.Count);
            Assert.AreEqual("Date and time value in --expire-at must be in the future. Value used: 2020-01-01.", parseResult.Errors[0].Message);
        }
    }

    [TestMethod]
    public void BuildRootCommand_WhenExpireAtParsedOnce_DoesNotRequeryClockOnGetValue()
    {
        FakeTimeProvider clock = new(
            new DateTimeOffset(2026, 11, 22, 16, 59, 59, 900, TimeSpan.Zero),
            TimeZoneInfo.Utc,
            TimeSpan.FromSeconds(1));
        RootCommand rootCommand = Program.BuildRootCommand(clock, CultureInfo.GetCultureInfo("en-US"));

        ParseResult parseResult = rootCommand.Parse(["--expire-at", "17:00:00"]);

        Assert.AreEqual(0, parseResult.Errors.Count);
        DateTimeOffset? result = parseResult.GetValue<DateTimeOffset?>("--expire-at");
        DateTimeOffset? secondRead = parseResult.GetValue<DateTimeOffset?>("--expire-at");
        DateTimeOffset expected = new(2026, 11, 22, 17, 0, 0, TimeSpan.Zero);
        Assert.IsNotNull(result);
        Assert.AreEqual(expected, result.Value);
        Assert.AreEqual(expected.Offset, result.Value.Offset);
        Assert.AreEqual(expected, secondRead);
        Assert.AreEqual(1, clock.UtcNowCallCount);
    }

    [TestMethod]
    public void BuildRootCommand_WhenExpireAtIsOmittedOrBare_HasNoErrorsAndReturnsNull()
    {
        string[][] cases =
        [
            [],
            ["--expire-at"],
        ];

        foreach (string[] args in cases)
        {
            FakeTimeProvider clock = new(new DateTimeOffset(2026, 11, 22, 19, 0, 0, TimeSpan.Zero), TimeZoneInfo.Utc);
            RootCommand rootCommand = Program.BuildRootCommand(clock, CultureInfo.GetCultureInfo("en-US"));

            ParseResult parseResult = rootCommand.Parse(args);

            Assert.AreEqual(0, parseResult.Errors.Count);
            Assert.IsNull(parseResult.GetValue<DateTimeOffset?>("--expire-at"));
            Assert.AreEqual(0, clock.UtcNowCallCount);
        }
    }

    [TestMethod]
    [DataRow(false, 0, false, -1, true)]
    [DataRow(false, 0, false, 0, true)]
    [DataRow(false, 0, false, 1, false)]
    [DataRow(true, 0, false, -1, false)]
    [DataRow(false, 1234, false, -1, false)]
    [DataRow(false, 0, true, -1, false)]
    public void IsStandaloneExpirationExpired_EvaluatesModeAndTimestampCorrectly(
        bool usePtConfig, int pid, bool useParentPid, int offsetSeconds, bool expected)
    {
        DateTimeOffset now = new(2026, 11, 22, 14, 0, 0, TimeSpan.Zero);
        DateTimeOffset? expireAt = now.AddSeconds(offsetSeconds);

        bool result = Program.IsStandaloneExpirationExpired(usePtConfig, pid, useParentPid, expireAt, now);

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void IsStandaloneExpirationExpired_WhenExpireAtIsNull_ReturnsFalse()
    {
        DateTimeOffset now = new(2026, 11, 22, 14, 0, 0, TimeSpan.Zero);

        bool result = Program.IsStandaloneExpirationExpired(false, 0, false, null, now);

        Assert.IsFalse(result);
    }
}
