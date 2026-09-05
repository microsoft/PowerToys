// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.ClipboardHistory.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.UnitTests;

[TestClass]
public class DetailsMarkdownHelperTests
{
    [TestMethod]
    public void BuildTextBody_PreservesLineBreaksAndIndentation()
    {
        var body = DetailsMarkdownHelper.BuildTextBody("first line\n    indented line\nlast line");

        Assert.AreEqual("```text\nfirst line\n    indented line\nlast line\n```", body);
    }

    [TestMethod]
    public void BuildTextBody_UsesLongerFence_WhenTextContainsFence()
    {
        var body = DetailsMarkdownHelper.BuildTextBody("before ``` after");

        Assert.AreEqual("````text\nbefore ``` after\n````", body);
    }

    [TestMethod]
    public void BuildImageBody_ReturnsMarkdownImage_WithFitAndMaxHeightHints()
    {
        var path = @"C:\Temp\clipboard.png";

        var body = DetailsMarkdownHelper.BuildImageBody(path, "Image");

        Assert.AreEqual("![Image](file:///C:/Temp/clipboard.png?--x-cmdpal-fit=fit&--x-cmdpal-maxheight=200)", body);
    }

    [TestMethod]
    public void BuildImageBody_UsesProvidedAltText()
    {
        var path = @"C:\Temp\clipboard.png";

        var body = DetailsMarkdownHelper.BuildImageBody(path, "Clipboard image");

        StringAssert.StartsWith(body, "![Clipboard image](file:///");
    }

    [TestMethod]
    public void BuildImageBody_ReturnsEmpty_WhenImageDataIsNull()
    {
        var body = DetailsMarkdownHelper.BuildImageBody(null, "Image");

        Assert.AreEqual(string.Empty, body);
    }
}
