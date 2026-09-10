// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.ApplicationModel.DataTransfer;
using Windows.Globalization;
using Windows.Media.Ocr;
using Button = Microsoft.PowerToys.UITest.Next.Button;
using WinClipboard = Windows.ApplicationModel.DataTransfer.Clipboard;

namespace AdvancedPaste.UITests;

[TestClass]
[DoNotParallelize]
[TestCategory("AdvancedPaste")]
public sealed class AdvancedPasteOcrTests : AdvancedPasteTestBase
{
    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    public async Task WindowsOcrPreviewsAndPastesImageText(bool fileInput, bool acceptWithEnter)
    {
        var source = CreateOcrImage();
        if (fileInput)
        {
            await SetFileClipboard(source);
        }
        else
        {
            await SetBitmapClipboard(source);
        }

        SelectAction(OpenAdvancedPaste(), "Image to text");
        var preview = Session.FromProcess(ProcessName);
        preview.Find<TextBlock>(By.Name(ClipboardFixtures.OcrText), 30_000);
        var paste = preview.Find<Button>(By.AccessibilityId("PreviewPasteBtn"), 15_000);
        Assert.AreEqual(string.Empty, Target.Text, "OCR pasted before the preview was accepted.");
        Assert.IsTrue(
            WinClipboard.GetContent().Contains(fileInput ? StandardDataFormats.StorageItems : StandardDataFormats.Bitmap),
            "The preview changed the clipboard before acceptance.");
        Step("Accepting the offline OCR preview");
        if (acceptWithEnter)
        {
            paste.Focus();
            SendShortcut(Key.Enter);
        }
        else
        {
            paste.Invoke(msPostAction: 0);
        }

        Target.AssertText(ClipboardFixtures.OcrText);
        Assert.AreEqual(ClipboardFixtures.OcrText, ReadClipboardText());
        WaitUntil(() => !IsAdvancedPasteVisible(), "Advanced Paste remained visible after accepting OCR.");
    }

    [TestMethod]
    public async Task WindowsOcrDirectShortcutPastesWithoutPreview()
    {
        await SetBitmapClipboard(CreateOcrImage());
        Target.Focus();
        Step("Executing the Image to text direct shortcut without a model/provider");
        SendShortcut(Key.Ctrl, Key.Alt, Key.LWin, Key.I);
        Target.AssertText(ClipboardFixtures.OcrText);
        Assert.AreEqual(ClipboardFixtures.OcrText, ReadClipboardText());
        WaitUntil(() => !IsAdvancedPasteVisible(), "Direct OCR unexpectedly left a preview open.");
    }

    [TestMethod]
    public async Task ImageWithoutTextReportsAnErrorWithoutChangingClipboard()
    {
        var source = Path.Combine(TestDirectory, "no-text.png");
        ClipboardFixtures.CreateImage(source);
        await SetBitmapClipboard(source);
        var window = OpenAdvancedPaste();
        SelectAction(window, "Image to text");
        window.Find<TextBlock>(By.Name("An error occurred during the paste operation"), 30_000);
        Assert.AreEqual(string.Empty, Target.Text, "Failed OCR pasted unexpected data.");
        Assert.IsTrue(WinClipboard.GetContent().Contains(StandardDataFormats.Bitmap), "Failed OCR replaced the original clipboard image.");
        Assert.IsTrue(IsAdvancedPasteVisible(), "The error unexpectedly closed Advanced Paste.");
        DismissAdvancedPaste();
        SetClipboardText("Still usable after an OCR error");
        SelectAction(OpenAdvancedPaste(), "Paste as plain text");
        Target.AssertText("Still usable after an OCR error");
    }

    private string CreateOcrImage()
    {
        Assert.IsTrue(OcrEngine.IsLanguageSupported(new Language("en-US")), "The Windows en-US OCR language is required; no external model is used.");
        var path = Path.Combine(TestDirectory, "ocr.png");
        ClipboardFixtures.CreateImage(path, withText: true);
        return path;
    }
}
