// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using System.Globalization;
using Awake.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Awake.UnitTests;

[TestClass]
public class CommandLineArgumentNormalizerTests
{
    private static readonly DateTime TestLocalTime = new(2026, 10, 13, 17, 0, 0, DateTimeKind.Unspecified);
    private static readonly DateTimeOffset Now = new(TestLocalTime, TimeZoneInfo.Local.GetUtcOffset(TestLocalTime));

    [DataRow("45m", 2700u)]
    [DataRow("2h", 7200u)]
    [DataRow("1h30m", 5400u)]
    [DataRow("1d2h3m4s", 93784u)]
    [DataRow("1H30M", 5400u)]
    [DataRow("4294967295s", uint.MaxValue)]
    [DataTestMethod]
    public void TryParseDurationSeconds_WithHumanDuration_ReturnsSeconds(string value, uint expected)
    {
        bool parsed = CommandLineArgumentNormalizer.TryParseDurationSeconds(value, out uint seconds);

        Assert.IsTrue(parsed);
        Assert.AreEqual(expected, seconds);
    }

    [DataRow("")]
    [DataRow("0m")]
    [DataRow("90")]
    [DataRow("1.5h")]
    [DataRow("h1")]
    [DataRow("1x")]
    [DataRow("4294967296s")]
    [DataRow("4294967295s1s")]
    [DataRow("18446744073709551615d")]
    [DataRow("-1h")]
    [DataRow("1m trailing")]
    [DataRow("1💤")]
    [DataTestMethod]
    public void TryParseDurationSeconds_WithInvalidDuration_ReturnsFalse(string value)
    {
        Assert.IsFalse(CommandLineArgumentNormalizer.TryParseDurationSeconds(value, out _));
    }

    [TestMethod]
    public void TryNormalize_ForCommand_PreservesFollowingOptions()
    {
        bool normalized = TryNormalize(
            ["for", "1h30m", "--display-on", "--pid", "42"],
            out string[] arguments,
            out CommandLineNormalizationError error);

        Assert.IsTrue(normalized);
        string[] expectedArguments = ["--time-limit", "5400", "--display-on", "--pid", "42"];
        CollectionAssert.AreEqual(expectedArguments, arguments);
        Assert.AreEqual(CommandLineNormalizationError.None, error);
    }

    [TestMethod]
    public void TryNormalize_UntilTime_UsesNextOccurrence()
    {
        bool normalized = TryNormalize(
            ["until", "16:30"],
            out string[] arguments,
            out CommandLineNormalizationError error);

        Assert.IsTrue(normalized);
        Assert.AreEqual("--expire-at", arguments[0]);
        DateTimeOffset expiration = DateTimeOffset.Parse(arguments[1], CultureInfo.InvariantCulture);
        Assert.AreEqual(new DateTimeOffset(2026, 10, 14, 16, 30, 0, TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 10, 14, 16, 30, 0))), expiration);
        Assert.AreEqual(CommandLineNormalizationError.None, error);
    }

    [TestMethod]
    public void TryNormalize_UntilDateTime_PreservesFutureInstant()
    {
        bool normalized = TryNormalize(
            ["until", "2026-10-14T18:00:00+08:00"],
            out string[] arguments,
            out CommandLineNormalizationError error);

        Assert.IsTrue(normalized);
        Assert.AreEqual(
            new DateTimeOffset(2026, 10, 14, 18, 0, 0, TimeSpan.FromHours(8)),
            DateTimeOffset.Parse(arguments[1], CultureInfo.InvariantCulture));
        Assert.AreEqual(CommandLineNormalizationError.None, error);
    }

    [TestMethod]
    public void TryNormalize_ParentAlias_UsesLegacyOption()
    {
        bool normalized = TryNormalize(
            ["for", "5m", "--parent"],
            out string[] arguments,
            out CommandLineNormalizationError error);

        Assert.IsTrue(normalized);
        string[] expectedArguments = ["--time-limit", "300", "--use-parent-pid"];
        CollectionAssert.AreEqual(expectedArguments, arguments);
        Assert.AreEqual(CommandLineNormalizationError.None, error);
    }

    [TestMethod]
    public void TryNormalize_ForWithoutDuration_ReturnsDurationRequired()
    {
        bool normalized = TryNormalize(["for"], out _, out CommandLineNormalizationError error);

        Assert.IsFalse(normalized);
        Assert.AreEqual(CommandLineNormalizationError.DurationRequired, error);
    }

    [TestMethod]
    public void TryNormalize_UntilWithoutTime_ReturnsTimeRequired()
    {
        bool normalized = TryNormalize(["until"], out _, out CommandLineNormalizationError error);

        Assert.IsFalse(normalized);
        Assert.AreEqual(CommandLineNormalizationError.TimeRequired, error);
    }

    [TestMethod]
    public void TryNormalize_CommandHelp_ShowsRootHelp()
    {
        bool normalized = TryNormalize(
            ["for", "--help"],
            out string[] arguments,
            out CommandLineNormalizationError error);

        Assert.IsTrue(normalized);
        string[] expectedArguments = ["--help"];
        CollectionAssert.AreEqual(expectedArguments, arguments);
        Assert.AreEqual(CommandLineNormalizationError.None, error);
    }

    [TestMethod]
    public void TryNormalize_LegacyOptions_RemainUnchanged()
    {
        string[] original = ["-c:true", "-d:false", "-t:60", "-p", "42", "-e", "2026-10-14T18:00:00+08:00", "-u"];
        Assert.IsTrue(TryNormalize(original, out string[] arguments, out _));
        CollectionAssert.AreEqual(original, arguments);
    }

    [DataRow("--time-limit")]
    [DataRow("-t:60")]
    [DataRow("--expire-at=2026-12-31")]
    [DataRow("--use-pt-config")]
    [DataRow("-c:true")]
    [DataTestMethod]
    public void TryNormalize_ModernCommandWithConflictingOption_IsRejected(string option)
    {
        Assert.IsFalse(TryNormalize(["for", "1h", option], out _, out var error));
        Assert.AreEqual(CommandLineNormalizationError.ConflictingOptions, error);
    }

    [TestMethod]
    public void TryNormalize_UntilPastDateTime_IsRejected()
    {
        Assert.IsFalse(TryNormalize(["until", "2020-01-01T00:00:00Z"], out _, out var error));
        Assert.AreEqual(CommandLineNormalizationError.TimeAlreadyPassed, error);
    }

    [DataRow("not a time")]
    [DataRow("2026-02-30T12:00:00")]
    [DataRow("💤")]
    [DataTestMethod]
    public void TryNormalize_UntilInvalidTime_IsRejected(string value)
    {
        Assert.IsFalse(TryNormalize(["until", value], out _, out var error));
        Assert.AreEqual(CommandLineNormalizationError.InvalidTime, error);
    }

    [TestMethod]
    public void TryParseExpiration_TomorrowAcrossDaylightSaving_RecalculatesOffset()
    {
        TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        DateTimeOffset now = new(2026, 3, 7, 18, 0, 0, TimeSpan.FromHours(-8));
        Assert.IsTrue(CommandLineArgumentNormalizer.TryParseExpiration(
            "17:30", now, CultureInfo.InvariantCulture, zone, out DateTimeOffset expiration));
        Assert.AreEqual(new DateTimeOffset(2026, 3, 8, 17, 30, 0, TimeSpan.FromHours(-7)), expiration);
    }

    [TestMethod]
    public void TryParseExpiration_NonexistentLocalTime_IsRejected()
    {
        TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        DateTimeOffset now = new(2026, 3, 7, 18, 0, 0, TimeSpan.FromHours(-8));
        Assert.IsFalse(CommandLineArgumentNormalizer.TryParseExpiration(
            "02:30", now, CultureInfo.InvariantCulture, zone, out _));
    }

    [DataRow("2026-11-01T00:30:00-07:00", "2026-11-01T01:30:00-07:00")]
    [DataRow("2026-11-01T01:15:00-07:00", "2026-11-01T01:30:00-07:00")]
    [DataRow("2026-11-01T01:30:00-07:00", "2026-11-01T01:30:00-08:00")]
    [DataRow("2026-11-01T01:45:00-07:00", "2026-11-01T01:30:00-08:00")]
    [DataRow("2026-11-01T01:15:00-08:00", "2026-11-01T01:30:00-08:00")]
    [DataRow("2026-11-01T01:30:00-08:00", "2026-11-02T01:30:00-08:00")]
    [DataRow("2026-11-01T02:00:00-08:00", "2026-11-02T01:30:00-08:00")]
    [DataTestMethod]
    public void TryParseExpiration_AutumnOverlap_SelectsEarliestFutureOccurrence(string currentTime, string expectedTime)
    {
        TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
        DateTimeOffset now = DateTimeOffset.Parse(currentTime, CultureInfo.InvariantCulture);
        DateTimeOffset expected = DateTimeOffset.Parse(expectedTime, CultureInfo.InvariantCulture);

        Assert.IsTrue(CommandLineArgumentNormalizer.TryParseExpiration(
            "01:30", now, CultureInfo.InvariantCulture, zone, out DateTimeOffset expiration));
        Assert.AreEqual(expected, expiration);
        Assert.AreEqual(expected.Offset, expiration.Offset);
    }

    [DataRow("--pid")]
    [DataRow("--expire-at")]
    [DataRow("--time-limit")]
    [DataTestMethod]
    public void RootCommand_OptionWithoutValue_IsRejected(string option)
    {
        var command = Program.BuildRootCommand();
        string[] arguments = [option];
        Assert.IsTrue(command.Parse(arguments).Errors.Count > 0);
    }

    [TestMethod]
    public void RootCommand_LegacyRunnerArguments_AreAccepted()
    {
        var command = Program.BuildRootCommand();
        string[] arguments = ["--use-pt-config", "--pid", "42"];
        Assert.AreEqual(0, command.Parse(arguments).Errors.Count);
    }

    [TestMethod]
    public void RootCommand_ParentAlias_IsAccepted()
    {
        var command = Program.BuildRootCommand();
        string[] arguments = ["--parent"];
        Assert.AreEqual(0, command.Parse(arguments).Errors.Count);
    }

    [TestMethod]
    public void TryParseDurationSeconds_ArbitraryUnicodeInput_DoesNotThrow()
    {
        Random random = new(51181);
        for (int sample = 0; sample < 1000; sample++)
        {
            char[] characters = new char[random.Next(0, 64)];
            for (int index = 0; index < characters.Length; index++)
            {
                characters[index] = (char)random.Next(char.MaxValue + 1);
            }

            if (CommandLineArgumentNormalizer.TryParseDurationSeconds(new string(characters), out uint seconds))
            {
                Assert.IsTrue(seconds > 0);
            }
            else
            {
                Assert.AreEqual(0u, seconds);
            }
        }
    }

    private static bool TryNormalize(
        string[] arguments,
        out string[] normalizedArguments,
        out CommandLineNormalizationError error)
    {
        return CommandLineArgumentNormalizer.TryNormalize(
            arguments,
            Now,
            CultureInfo.InvariantCulture,
            out normalizedArguments,
            out error,
            out _);
    }
}
