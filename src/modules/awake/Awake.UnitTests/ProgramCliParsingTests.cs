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
    private static readonly CultureInfo UsCulture = CultureInfo.GetCultureInfo("en-US");
    private static readonly TimeZoneInfo Eastern = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
    private static readonly DateTimeOffset BaseUtcNow = new(2026, 11, 22, 19, 0, 0, TimeSpan.Zero);

    [TestMethod]
    [DataRow("--expire-at", "2025-01-01 14:05")]
    [DataRow("--expire-at=2025-01-01 14:05")]
    [DataRow("-e", "2025-01-01 14:05")]
    public void BuildRootCommand_PastExplicitDate_ReportsFutureError(params string[] args)
    {
        ParseResult parseResult = Parse(args, Eastern);

        Assert.AreEqual(1, parseResult.Errors.Count);
        Assert.AreEqual("Date and time value in --expire-at must be in the future. Value used: 2025-01-01 14:05.", parseResult.Errors[0].Message);
    }

    [TestMethod]
    [DataRow("--expire-at", "not-a-date")]
    [DataRow("-e=not-a-date")]
    public void BuildRootCommand_UnparseableValue_PreservesExistingErrorText(params string[] args)
    {
        ParseResult parseResult = Parse(args, TimeZoneInfo.Utc);

        Assert.AreEqual(1, parseResult.Errors.Count);
        Assert.IsTrue(
            parseResult.Errors[0].Message.Contains("Date and time value in --expire-at could not be parsed correctly", StringComparison.Ordinal),
            $"Unexpected error message: {parseResult.Errors[0].Message}");
    }

    [TestMethod]
    [DataRow(0, "--expire-at=00:00:00")]
    [DataRow(8, "-e", "08:00")]
    public void BuildRootCommand_PastTimeOnly_RollsToTomorrowWithNoErrors(int expectedHour, params string[] args)
    {
        ParseResult parseResult = Parse(args, Eastern);

        Assert.AreEqual(0, parseResult.Errors.Count);
        DateTimeOffset? result = parseResult.GetValue<DateTimeOffset?>("--expire-at");
        DateTimeOffset expected = new(2026, 11, 23, expectedHour, 0, 0, TimeSpan.FromHours(-5));
        Assert.IsNotNull(result);
        Assert.AreEqual(expected, result.Value);
        Assert.AreEqual(expected.Offset, result.Value.Offset);
    }

    [TestMethod]
    [DataRow]
    [DataRow("--expire-at")]
    public void BuildRootCommand_OmittedOrBareExpireAt_ReturnsNullWithNoErrors(params string[] args)
    {
        ParseResult parseResult = Parse(args, TimeZoneInfo.Utc);

        Assert.AreEqual(0, parseResult.Errors.Count);
        Assert.IsNull(parseResult.GetValue<DateTimeOffset?>("--expire-at"));
    }

    [TestMethod]
    [DataRow("--use-pt-config", "--expire-at", "2020-01-01")]
    [DataRow("--pid", "1234", "--expire-at", "2020-01-01")]
    public void BuildRootCommand_PastExpireAtWithConfigOrPid_ReportsFutureError(params string[] args)
    {
        ParseResult parseResult = Parse(args, TimeZoneInfo.Utc);

        Assert.AreEqual(1, parseResult.Errors.Count);
        Assert.AreEqual("Date and time value in --expire-at must be in the future. Value used: 2020-01-01.", parseResult.Errors[0].Message);
    }

    private static ParseResult Parse(string[] args, TimeZoneInfo timeZone)
    {
        RootCommand rootCommand = Program.BuildRootCommand(new FixedTimeProvider(BaseUtcNow, timeZone), UsCulture);
        return rootCommand.Parse(args);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow, TimeZoneInfo localTimeZone)
        {
            _utcNow = utcNow;
            LocalTimeZone = localTimeZone;
        }

        public override TimeZoneInfo LocalTimeZone { get; }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
