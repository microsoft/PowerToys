// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.Helpers;

namespace Microsoft.CmdPal.Common.UnitTests.Helpers;

[Microsoft.VisualStudio.TestTools.UnitTesting.OSCondition(Microsoft.VisualStudio.TestTools.UnitTesting.OperatingSystems.Windows)]
[TestClass]
public class ShellArgumentBuilderTests
{
    [TestMethod]
    [DataRow("plain", "plain")]
    [DataRow("C:\\Program Files\\PowerToys", "\"C:\\Program Files\\PowerToys\"")]
    [DataRow("say \"hello\"", "\"say \\\"hello\\\"\"")]
    [DataRow("", "\"\"")]
    [DataRow("C:\\Program Files\\", "\"C:\\Program Files\\\\\"")]
    public void BuildArguments_FormatsSingleArgument(string argument, string expected)
    {
        var actual = ShellArgumentBuilder.BuildArguments(argument);

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void BuildArguments_FormatsMultipleArguments()
    {
        var actual = ShellArgumentBuilder.BuildArguments("plain", "C:\\Program Files\\PowerToys", "two words");

        Assert.AreEqual("plain \"C:\\Program Files\\PowerToys\" \"two words\"", actual);
    }
}
