// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Peek.UITests;

[TestClass]
public class PeekFilePreviewTests : UITestBase
{
    public PeekFilePreviewTests()
        : base(PowerToysModule.PowerToysSettings, WindowSize.Small_Vertical)
    {
    }

    [TestMethod("Peek.FilePreview.Folder")]
    [TestCategory("PeekFilePreview")]
    public void PeekFolderFilePreview()
    {
        string folderFullPath = Path.GetTempPath();

        OpenAndPeekFile(folderFullPath, "Temp - Peek");
        Assert.IsTrue(FindAll<TextBlock>("File Type: File folder", 500, true).Count > 0, "Should show folder detail in Peek File Preview");
    }

    [TestMethod("Peek.FilePreview.Image")]
    [TestCategory("PeekFilePreview")]
    public void PeekImageFilePreview() => PeekSampleFile(".png");

    [TestMethod("Peek.FilePreview.PDF")]
    [TestCategory("PeekFilePreview")]
    public void PeekPDFPreview() => PeekSampleFile(".pdf");

    [TestMethod("Peek.FilePreview.ZIP")]
    [TestCategory("PeekFilePreview")]
    public void PeekZIPPreview() => PeekSampleFile(".zip");

    [TestMethod("Peek.FilePreview.QOI")]
    [TestCategory("PeekFilePreview")]
    public void PeekQOIPreview() => PeekSampleFile(".qoi");

    private void PeekSampleFile(string ext)
    {
        string fileName = $"sample{ext}";
        string fullPath = Path.GetFullPath($@".\TestAssets\{fileName}");

        OpenAndPeekFile(fullPath, $"{fileName} - Peek");
        VisualAssertWindow($"{fileName} - Peek");
    }

    private void OpenAndPeekFile(string fullPath, string peekWindowTitle)
    {
        SendKeys(Key.Enter);

        Session.StartExe("explorer.exe", $"/select,\"{fullPath}\"");

        SendKeys(Key.LCtrl, Key.Space);

        Session.Attach(peekWindowTitle);
    }

    private void VisualAssertWindow(string windowTitle)
    {
        VisualAssert.AreEqual(this.TestContext, this.Find(windowTitle, 500, true), "EmptyView");
    }
}
