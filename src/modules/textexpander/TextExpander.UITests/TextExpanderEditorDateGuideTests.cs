// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UITests;

/// <summary>Checks the date guide through the editor, including which format syntax its chips can edit.</summary>
[TestClass]
public sealed class TextExpanderEditorDateGuideTests : TextExpanderTestBase
{
    /// <summary>The .NET guide is hidden for strftime, while its ordinary snippet preview keeps working.</summary>
    [TestMethod("TextExpander.Editor.ShowsDateGuideOnlyForDotNetFormats")]
    [TestCategory("TextExpander")]
    [TestCategory("TextExpander.Editor")]
    public void ShowsDateGuideOnlyForDotNetFormats()
    {
        const string dotNet = "{{date:yyyy-MM-dd}}";
        const string strftime = "{{date:%Y-%m-%d}}";
        const string heading = "Date format codes";
        var editor = OpenEditor(OpenTextExpanderPage());
        editor.WaitForCount(DefaultSnippets.Count);
        editor.OpenNewSnippet();
        editor.SetTrigger(";tedateguide");

        editor.TypeReplacement(dotNet);
        Assert.IsTrue(editor.WaitForReplacementText(dotNet));
        TextInput.SelectAll();
        Assert.IsTrue(editor.WaitForText(heading), "The guide should be available for a .NET date format.");

        var before = DateTime.Now;
        editor.TypeReplacement(strftime);
        Assert.IsTrue(editor.WaitForReplacementText(strftime));
        TextInput.SelectAll();
        Assert.IsTrue(editor.Session.WaitForExactNameGone(heading, 10_000), "The .NET chips should not be offered for strftime.");
        Assert.IsTrue(
            editor.Session.WaitFor(
                () => new[] { before, DateTime.Now }.Any(date => editor.Session.FindExact<Element>(
                    $"({date.ToString("yyyy-MM-dd", CultureInfo.CurrentCulture)}, filled in when the snippet runs)").Count > 0),
                10_000,
                pollIntervalMS: 250),
            "The strftime snippet should retain its formatted preview while the guide is hidden.");
        Assert.AreEqual(strftime, editor.ReplacementText, "Hiding the guide must leave the template unchanged.");

        editor.TypeReplacement(dotNet);
        Assert.IsTrue(editor.WaitForReplacementText(dotNet));
        TextInput.SelectAll();
        Assert.IsTrue(editor.WaitForText(heading), "The guide should return after switching back to a .NET format.");
    }
}
