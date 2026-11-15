// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.ApplicationModel.DataTransfer;

namespace Microsoft.CommandPalette.Extensions.Toolkit.UnitTests;

[TestClass]
[DoNotParallelize]
public class ClipboardHelperTests
{
    [TestMethod]
    [DataRow("ASCII clipboard text")]
    [DataRow("Caf\u00e9, \u4e16\u754c, \ud83d\ude80\r\nSecond line.")]
    public void SetText_RoundTripsThroughClipboard(string text)
    {
        var value = $"{text} {Guid.NewGuid():N}";

        ClipboardHelper.SetText(value);

        Assert.AreEqual(value, ClipboardHelper.GetText());
    }

    [STATestMethod]
    public async Task SetRtf_SetsRichTextAndPlainText()
    {
        var plainText = $"Command Palette RTF clipboard test {Guid.NewGuid():N}";
        var rtfText = $@"{{\rtf1\ansi \b {plainText}\b0}}";

        ClipboardHelper.SetRtf(plainText, rtfText);

        Assert.AreEqual(plainText, ClipboardHelper.GetText());
        var content = Clipboard.GetContent();
        Assert.IsTrue(content.Contains(StandardDataFormats.Rtf));
        Assert.AreEqual(rtfText, await content.GetRtfAsync());
    }

    [TestMethod]
    public void SetImage_RejectsNullReference()
    {
        Assert.ThrowsException<ArgumentNullException>(() => ClipboardHelper.SetImage(null!));
    }

    [TestMethod]
    public void SetContent_RejectsNullPackage()
    {
        Assert.ThrowsException<ArgumentNullException>(() => ClipboardHelper.SetContent(null!));
    }
}
