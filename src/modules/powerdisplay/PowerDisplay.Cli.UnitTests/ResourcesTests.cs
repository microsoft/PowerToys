// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Cli.Options;
using PowerDisplay.Cli.Properties;

namespace PowerDisplay.Cli.UnitTests;

[OSCondition(OperatingSystems.Windows)]
[TestClass]
public class ResourcesTests
{
    private const string ExecutableName = "PowerToys.PowerDisplay.Cli.exe";

    [TestMethod]
    public void CommandHints_ReferenceShippedExecutable()
    {
        Assert.Contains(ExecutableName, Resources.Hint_UseSetForAbsolute);
        Assert.Contains(ExecutableName, Resources.Hint_UseHexVcp);
        Assert.Contains(ExecutableName, Resources.Hint_RunList);
        Assert.Contains(ExecutableName, Resources.Hint_SelectorMissing);
        Assert.Contains(ExecutableName, Resources.Hint_RunProfiles);
    }

    [TestMethod]
    public void OptionDescriptions_ReferenceShippedExecutable()
    {
        string?[] descriptions =
        [
            CliOptions.MonitorNumber.Description,
            CliOptions.ColorTemperature.Description,
            CliOptions.InputSource.Description,
            CliOptions.PowerState.Description,
            CliOptions.ProfileId.Description,
        ];

        foreach (var description in descriptions)
        {
            Assert.IsNotNull(description);
            Assert.Contains(ExecutableName, description);
        }
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
