// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Cli;
using PowerDisplay.Cli.Commands;
using PowerDisplay.Cli.Options;
using PowerDisplay.Contracts;

namespace PowerDisplay.Cli.UnitTests;

[TestClass]
public class ProgramTokenTests
{
    private static ParseResult Parse(params string[] args)
        => new Parser(new PowerDisplayRootCommand()).Parse(args);

    [TestMethod]
    public void HelpFlag_IsDetected()
        => Assert.IsTrue(Program.HasHelpToken(Parse("--help")));

    [TestMethod]
    public void HelpUnderSubcommand_IsDetected()
        => Assert.IsTrue(Program.HasHelpToken(Parse("get", "--help")));

    [TestMethod]
    public void HelpValueOfOption_IsNotTreatedAsHelp()
        => Assert.IsFalse(Program.HasHelpToken(Parse("set", "-i", "-h", "--brightness", "50")));

    [TestMethod]
    public void HelpUnderApplyProfile_IsDetected()
        => Assert.IsTrue(Program.HasHelpToken(Parse("apply-profile", "--help")));

    [TestMethod]
    public void ApplyProfileWithRealName_IsNotHelp()
        => Assert.IsFalse(Program.HasHelpToken(Parse("apply-profile", "Night")));

    [TestMethod]
    public void VersionFlag_IsDetected()
        => Assert.IsTrue(Program.HasVersionToken(Parse("--version")));

    [TestMethod]
    public void VersionFlag_DetectedAlongsideValidOptions()
        => Assert.IsTrue(Program.HasVersionToken(Parse("set", "-n", "1", "--version")));

    [TestMethod]
    public void VersionValueOfOption_IsNotTreatedAsVersion()
        => Assert.IsFalse(Program.HasVersionToken(Parse("set", "-i", "--version", "--brightness", "50")));

    [TestMethod]
    public void IsVersionRequest_BareVersion_True()
        => Assert.IsTrue(Program.IsVersionRequest(Parse("--version")));

    [TestMethod]
    public void IsVersionRequest_VersionAfterSubcommand_False()
        => Assert.IsFalse(Program.IsVersionRequest(Parse("set", "-n", "1", "--version")));

    [TestMethod]
    public void IsVersionRequest_VersionUnderApplyProfile_True()
    {
        // `apply-profile <name>` greedily binds "--version" as the profile name, so it never reaches
        // UnmatchedTokens. It must still be treated as a version request (mirrors the --help carve-out)
        // rather than dispatched as "apply a profile literally named --version".
        Assert.IsTrue(Program.IsVersionRequest(Parse("apply-profile", "--version")));
    }

    [TestMethod]
    public void ApplyProfileWithRealName_IsNotVersion()
        => Assert.IsFalse(Program.IsVersionRequest(Parse("apply-profile", "Night")));

    [TestMethod]
    public void BuildParseErrorResult_CollapsesMultipleMessagesIntoOneEnvelope()
    {
        // System.CommandLine can report several errors for one bad invocation; they must be
        // collapsed into a single envelope so consumers receive one parseable object.
        var messages = new[] { "first problem", "second problem" };
        var result = Program.BuildParseErrorResult("set", messages);

        Assert.AreEqual("set", result.Command);
        Assert.AreEqual(CliErrorCodes.ArgumentError, result.Error.Code);
        Assert.AreEqual(CliExitCodes.ArgumentError, result.Error.ExitCode);
        StringAssert.Contains(result.Error.Message, "first problem");
        StringAssert.Contains(result.Error.Message, "second problem");
    }

    [TestMethod]
    public void BuildParseErrorResult_EmptyMessages_FallsBackToGenericMessage()
    {
        var blanks = new[] { string.Empty, "  " };
        var result = Program.BuildParseErrorResult("get", blanks);
        Assert.AreEqual("invalid arguments", result.Error.Message);
    }

    [TestMethod]
    public void Step_Negative_ProducesParseError()
    {
        var parsed = Parse("up", "--brightness", "--step", "-5");
        Assert.IsTrue(parsed.Errors.Count > 0, "a negative --step must be a parse error");
    }

    [TestMethod]
    public void Step_Zero_IsAccepted()
    {
        var parsed = Parse("up", "--brightness", "--step", "0");
        Assert.AreEqual(0, parsed.Errors.Count, "--step 0 is a valid no-op and must not error");
    }

    [TestMethod]
    public void Up_BrightnessFlag_ParsesWithoutValue()
    {
        var parsed = Parse("up", "--brightness");
        Assert.AreEqual(0, parsed.Errors.Count);
        Assert.IsTrue(parsed.GetValueForOption(CliOptions.BrightnessFlag));
    }

    [TestMethod]
    public void Up_BrightnessFlag_RejectsAttachedValue()
    {
        // The up/down setting flags are pure presence flags (ArgumentArity.Zero). A following
        // bareword like "false" must NOT be swallowed as the flag's value (which would silently make
        // the flag false and yield a misleading "no setting specified"); it is an unrecognized token.
        var parsed = Parse("up", "--brightness", "false");
        Assert.IsTrue(parsed.Errors.Count > 0, "an attached value on a no-value flag must be a parse error");
    }

    [TestMethod]
    public void Quiet_DoesNotSwallowFollowingProfileName()
    {
        // Regression: --quiet is a global Option<bool>. With ArgumentArity.Zero it must NOT swallow a
        // following bareword that parses as a bool, so `apply-profile --quiet true` binds "true" as the
        // profile name (not as --quiet's value, which would leave apply-profile with no name).
        var parsed = Parse("apply-profile", "--quiet", "true");

        Assert.AreEqual(0, parsed.Errors.Count, "--quiet must not consume the profile name");
        Assert.AreEqual("true", parsed.GetValueForArgument(CliOptions.ProfileName));
        Assert.IsTrue(parsed.GetValueForOption(CliOptions.Quiet), "a bare --quiet resolves to true");
    }

    [TestMethod]
    public void ConfirmPowerOff_ResolvesToTrueWhenPresent()
    {
        // --confirm-power-off is a pure presence flag (ArgumentArity.Zero): present -> true, and it
        // does not swallow the following power-state value.
        var parsed = Parse("set", "--power-state", "0x04", "--confirm-power-off");

        Assert.AreEqual(0, parsed.Errors.Count);
        Assert.IsTrue(parsed.GetValueForOption(CliOptions.ConfirmPowerOff));
        Assert.AreEqual("0x04", parsed.GetValueForOption(CliOptions.PowerState));
    }

    [TestMethod]
    public void ConnectTimeout_IsStrictlyShorterThanOperationTimeout()
    {
        // Guards the connect-timeout fix: the pipe-connect bound must stay strictly below the overall
        // deadline, or a not-running app is misreported as TIMEOUT (exit 8) after the full deadline
        // instead of a fast PROVIDER_UNAVAILABLE (exit 10). See Program.ConnectTimeout / OperationTimeout.
        Assert.IsTrue(
            Program.ConnectTimeout < Program.OperationTimeout,
            $"ConnectTimeout ({Program.ConnectTimeout}) must be < OperationTimeout ({Program.OperationTimeout})");
    }
}
