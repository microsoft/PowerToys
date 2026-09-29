// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UnitTests;

[TestClass]
public sealed class VariableTests
{
    [TestMethod]
    public void CustomDateFormatsSupportDotNetAndStrftime()
    {
        var date = new DateTime(2026, 9, 9, 14, 5, 7);
        Assert.AreEqual("09/09/2026", Variables.FormatDate(date, "MM/dd/yyyy"));
        Assert.AreEqual(Variables.FormatDate(date, "MM/dd/yyyy"), Variables.FormatDate(date, "%m/%d/%Y"));
        Assert.AreEqual("Wednesday", Variables.FormatDate(date, "dddd"));
        Assert.AreEqual("100% sure", Variables.FormatDate(date, "100%% sure"));
        Assert.AreEqual("252", Variables.FormatDate(date, "%j"));
        Assert.AreEqual("\"", Variables.FormatDate(date, "\""));
    }

    [TestMethod]
    public void ClipboardVariableIsInsertedVerbatimAndOnlyWhenRequested()
    {
        Assert.AreEqual("[pasted]", Variables.Expand("[{{clipboard}}]", _ => null, () => "pasted"));
        Assert.AreEqual("[]", Variables.Expand("[{{clipboard}}]", _ => null));
        Assert.AreEqual("{{date}} and \\n literal", Variables.Expand("{{clipboard}}", _ => null, () => "{{date}} and \\n literal"));

        int reads = 0;
        string? ReadClipboard()
        {
            reads++;
            return "x";
        }

        _ = Variables.Expand("{{date}} only", _ => null, ReadClipboard);
        Assert.AreEqual(0, reads);
    }

    [TestMethod]
    public void PromptVariablesAreDetectedAndExpandedPerOccurrence()
    {
        int count = 0;
        string expanded = Variables.Expand("{{prompt:Name}} {{prompt:Name}}", label => label + ++count);
        Assert.AreEqual("Name1 Name2", expanded);
        Assert.IsTrue(Variables.NeedsPrompt("before {{prompt:Name}} after"));
    }
}
