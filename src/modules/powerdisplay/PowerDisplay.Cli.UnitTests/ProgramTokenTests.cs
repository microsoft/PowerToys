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
}
