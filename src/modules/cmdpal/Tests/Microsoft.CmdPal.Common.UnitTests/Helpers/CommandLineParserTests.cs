// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using Microsoft.CmdPal.Common.Helpers;

namespace Microsoft.CmdPal.Common.UnitTests.Helpers;

[TestClass]
public class CommandLineParserTests
{
    [TestMethod]
    [DataRow("--settings", true)]
    [DataRow("\"C:\\Program Files\\PowerToys\\Microsoft.CmdPal.UI.exe\" --settings", true)]
    [DataRow("app.exe \"--settings\"", true)]
    [DataRow("\"C:\\--settings\\app.exe\"", false)]
    [DataRow("app.exe \"value --settings\"", false)]
    [DataRow("app.exe --settings-extra", false)]
    [DataRow("app.exe --Settings", false)]
    [DataRow("app.exe", false)]
    [DataRow("\"--settings\"suffix", false)]
    [DataRow("--set\"tings\"", true)]
    [DataRow("", false)]
    [DataRow("   ", false)]
    public void ParseArguments_MatchesSettingsAsWholeArgument(string activationArguments, bool expected)
    {
        var arguments = CommandLineParser.ParseArguments(activationArguments);

        Assert.AreEqual(expected, arguments.Contains("--settings", StringComparer.Ordinal));
    }

    [TestMethod]
    public void Parse_PreservesQuotedAndEmptyArguments()
    {
        var arguments = CommandLineParser.Parse(
            """
            app.exe "two words" "" "C:\path with spaces\\" a\"b "příliš žluťoučký"
            """);

        string[] expected = ["app.exe", "two words", string.Empty, "C:\\path with spaces\\", "a\"b", "příliš žluťoučký"];
        CollectionAssert.AreEqual(expected, arguments);
    }

    [TestMethod]
    public void ParseArguments_UsesArgumentQuotingForFirstToken()
    {
        var arguments = CommandLineParser.ParseArguments("a\\\"b \"\"");

        string[] expected = ["a\"b", string.Empty];
        CollectionAssert.AreEqual(expected, arguments);
    }
}
