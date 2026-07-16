// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Cli.Options;
using PowerDisplay.Cli.Properties;

namespace PowerDisplay.Cli.UnitTests;

[TestClass]
public class ResourcesTests
{
    private const string ExecutableName = "PowerToys.PowerDisplay.Cli.exe";

    [TestMethod]
    public void CommandHints_ReferenceShippedExecutable()
    {
        StringAssert.Contains(Resources.Hint_UseSetForAbsolute, ExecutableName);
        StringAssert.Contains(Resources.Hint_UseHexVcp, ExecutableName);
        StringAssert.Contains(Resources.Hint_RunList, ExecutableName);
        StringAssert.Contains(Resources.Hint_SelectorMissing, ExecutableName);
        StringAssert.Contains(Resources.Hint_RunProfiles, ExecutableName);
    }

    [TestMethod]
    public void OptionDescriptions_ReferenceShippedExecutable()
    {
        StringAssert.Contains(CliOptions.MonitorNumber.Description, ExecutableName);
        StringAssert.Contains(CliOptions.ColorTemperature.Description, ExecutableName);
        StringAssert.Contains(CliOptions.InputSource.Description, ExecutableName);
        StringAssert.Contains(CliOptions.PowerState.Description, ExecutableName);
        StringAssert.Contains(CliOptions.ProfileId.Description, ExecutableName);
    }

    [TestMethod]
    public void SafeFormat_PlaceholderIndexOutOfRange_DoesNotThrow_ReturnsTemplate()
    {
        // A translation that renumbers a placeholder ({0} -> {1}) leaves an index with no argument;
        // the guarantee is "degrade to the template, never throw".
        Assert.AreEqual("value {1}", Resources.SafeFormat("value {1}", "x"));
    }

    [TestMethod]
    public void SafeFormat_UnescapedBrace_DoesNotThrow_ReturnsTemplate()
    {
        // A translation with an unescaped brace is also a malformed format string.
        Assert.AreEqual("oops {", Resources.SafeFormat("oops {", "x"));
    }

    [TestMethod]
    public void SafeFormat_WellFormedTemplate_SubstitutesArgument()
    {
        // The success path must actually substitute — without this, a regression to `return template;`
        // would silently drop every {0}/{1} from localized messages while the malformed-template tests
        // above stayed green (a malformed template returns unchanged either way).
        Assert.AreEqual("value x", Resources.SafeFormat("value {0}", "x"));
        Assert.AreEqual("a then b", Resources.SafeFormat("{0} then {1}", "a", "b"));
    }
}
