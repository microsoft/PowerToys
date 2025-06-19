﻿// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

using Microsoft.AdvancedPaste.UITests.Helper;
using Microsoft.CodeCoverage.Core.Reports.Coverage;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using Windows.ApplicationModel.DataTransfer;
using static System.Net.Mime.MediaTypeNames;
using static System.Resources.ResXFileRef;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ToolTip;

namespace Microsoft.AdvancedPaste.UITests
{
    [TestClass]
    public class AdvancedPasteUITest : UITestBase
    {
        private readonly string testFilesFolderPath;
        private readonly string tempRTFFileName = "TempFile.rtf";
        private readonly string pasteAsPlainTextRawFileName = "PasteAsPlainTextFileRaw.rtf";
        private readonly string pasteAsPlainTextPlainFileName = "PasteAsPlainTextFilePlain.rtf";
        private readonly string pasteAsPlainTextPlainNoRepeatFileName = "PasteAsPlainTextFilePlainNoRepeat.rtf";
        private readonly string wordpadPath = @"C:\Program Files\wordpad\wordpad.exe";

        private readonly string tempTxtFileName = "TempFile.txt";
        private readonly string pasteAsMarkdownSrcFile = "PasteAsMarkdownFile.html";
        private readonly string pasteAsMarkdownResultFile = "PasteAsMarkdownResultFile.txt";

        private readonly string pasteAsJsonFileName = "PasteAsJsonFile.xml";
        private readonly string pasteAsJsonResultFile = "PasteAsJsonResultFile.txt";

        public AdvancedPasteUITest()
            : base(PowerToysModule.PowerToysSettings, size: WindowSize.Small)
        {
            Type currentTestType = typeof(AdvancedPasteUITest);
            string? dirName = Path.GetDirectoryName(currentTestType.Assembly.Location);
            Assert.IsNotNull(dirName, "Failed to get directory name of the current test assembly.");

            string testFilesFolder = Path.Combine(dirName, "TestFiles");
            Assert.IsTrue(Directory.Exists(testFilesFolder), $"Test files directory not found at: {testFilesFolder}");

            testFilesFolderPath = testFilesFolder;
        }

        [TestMethod]
        [TestCategory("UITest")]
        [TestCategory("AdvancedPaste")]
        public void TestWordDocumentCopyPaste()
        {
            TestCasePasteAsPlainText();
            TestCasePasteAsMarkdown();
            TestCasePasteAsJSON();
        }

        private void TestCasePasteAsPlainText()
        {
            // Copy some rich text(e.g word of the text is different color, another work is bold, underlined, etd.).
            // Paste the text using standard Windows Ctrl + V shortcut and ensure that rich text is pasted(with all colors, formatting, etc.)
            DeleteAndCopyFile(pasteAsPlainTextRawFileName, tempRTFFileName);
            ContentCopyAndPasteDirectly(tempRTFFileName, isRTF: true);

            var resultWithFormatting = FileReader.CompareRtfFiles(
                Path.Combine(testFilesFolderPath, tempRTFFileName),
                Path.Combine(testFilesFolderPath, pasteAsPlainTextRawFileName),
                compareFormatting: true);

            Assert.IsTrue(resultWithFormatting.IsConsistent, "RTF files should be identical including formatting");

            // Paste the text using Paste As Plain Text activation shortcut and ensure that plain text without any formatting is pasted.
            // Paste again the text using standard Windows Ctrl + V shortcut and ensure the text is now pasted plain without formatting as well.
            DeleteAndCopyFile(pasteAsPlainTextRawFileName, tempRTFFileName);
            ContentCopyAndPasteWithShortcutThenPasteAgain(tempRTFFileName, isRTF: true, Key.Win, Key.LCtrl, Key.Alt, Key.V);
            resultWithFormatting = FileReader.CompareRtfFiles(
                Path.Combine(testFilesFolderPath, tempRTFFileName),
                Path.Combine(testFilesFolderPath, pasteAsPlainTextPlainFileName),
                compareFormatting: true);
            Assert.IsTrue(resultWithFormatting.IsConsistent, "RTF files should be identical without formatting");

            // Copy some rich text again.
            // Open Advanced Paste window using hotkey, click Paste as Plain Text button and confirm that plain text without any formatting is pasted.
            DeleteAndCopyFile(pasteAsPlainTextRawFileName, tempRTFFileName);
            ContentCopyAndPasteCase3(tempRTFFileName, isRTF: true);
            resultWithFormatting = FileReader.CompareRtfFiles(
                Path.Combine(testFilesFolderPath, tempRTFFileName),
                Path.Combine(testFilesFolderPath, pasteAsPlainTextPlainNoRepeatFileName),
                compareFormatting: true);
            Assert.IsTrue(resultWithFormatting.IsConsistent, "RTF files should be identical without formatting");

            // Copy some rich text again.
            // Open Advanced Paste window using hotkey, press Ctrl + 1 and confirm that plain text without any formatting is pasted.
            DeleteAndCopyFile(pasteAsPlainTextRawFileName, tempRTFFileName);
            ContentCopyAndPasteCase4(tempRTFFileName, isRTF: true);
            resultWithFormatting = FileReader.CompareRtfFiles(
                Path.Combine(testFilesFolderPath, tempRTFFileName),
                Path.Combine(testFilesFolderPath, pasteAsPlainTextPlainNoRepeatFileName),
                compareFormatting: true);
            Assert.IsTrue(resultWithFormatting.IsConsistent, "RTF files should be identical without formatting");
        }

        private void TestCasePasteAsMarkdown()
        {
            // TODO: Open Settings and set Paste as Markdown directly hotkey

            // Copy some text(e.g.some HTML text - convertible to Markdown)
            // Paste the text using set hotkey and confirm that pasted text is converted to markdown
            DeleteAndCopyFile(pasteAsMarkdownSrcFile, tempTxtFileName);
            ContentCopyAndPasteAsMarkdownCase1(tempTxtFileName);
            var result = FileReader.CompareRtfFiles(
                Path.Combine(testFilesFolderPath, tempTxtFileName),
                Path.Combine(testFilesFolderPath, pasteAsMarkdownResultFile),
                compareFormatting: true);
            Assert.IsTrue(result.IsConsistent, "Paste as markdown using shortcut failed.");

            // Copy some text(same as in the previous step or different.If nothing is coppied between steps, previously pasted Markdown text will be picked up from clipboard and converted again to nested Markdown).
            // Open Advanced Paste window using hotkey, click Paste as markdown button and confirm that pasted text is converted to markdown
            DeleteAndCopyFile(pasteAsMarkdownSrcFile, tempTxtFileName);
            ContentCopyAndPasteAsMarkdownCase2(tempTxtFileName);
            result = FileReader.CompareRtfFiles(
                Path.Combine(testFilesFolderPath, tempTxtFileName),
                Path.Combine(testFilesFolderPath, pasteAsMarkdownResultFile),
                compareFormatting: true);
            Assert.IsTrue(result.IsConsistent, "Paste as markdown using shortcut failed.");

            // Copy some text(same as in the previous step or different.If nothing is coppied between steps, previously pasted Markdown text will be picked up from clipboard and converted again to nested Markdown).
            // Open Advanced Paste window using hotkey, press Ctrl + 2 and confirm that pasted text is converted to markdown
            DeleteAndCopyFile(pasteAsMarkdownSrcFile, tempTxtFileName);
            ContentCopyAndPasteAsMarkdownCase3(tempTxtFileName);
            result = FileReader.CompareRtfFiles(
                Path.Combine(testFilesFolderPath, tempTxtFileName),
                Path.Combine(testFilesFolderPath, pasteAsMarkdownResultFile),
                compareFormatting: true);
            Assert.IsTrue(result.IsConsistent, "Paste as markdown using shortcut failed.");
        }

        private void TestCasePasteAsJSON()
        {
            // TODO: Open Settings and set Paste as JSON directly hotkey

            // Copy some XML or CSV text(or any other text, it will be converted to simple JSON object)
            // Paste the text using set hotkey and confirm that pasted text is converted to JSON
            DeleteAndCopyFile(pasteAsJsonFileName, tempTxtFileName);
            ContentCopyAndPasteAsJsonCase1(tempTxtFileName);
            var result = FileReader.CompareRtfFiles(
                Path.Combine(testFilesFolderPath, tempTxtFileName),
                Path.Combine(testFilesFolderPath, pasteAsJsonResultFile),
                compareFormatting: true);
            Assert.IsTrue(result.IsConsistent, "Paste as Json using shortcut failed.");

            // Copy some text(same as in the previous step or different.If nothing is coppied between steps, previously pasted JSON text will be picked up from clipboard and converted again to nested JSON).
            // Open Advanced Paste window using hotkey, click Paste as markdown button and confirm that pasted text is converted to markdown
            DeleteAndCopyFile(pasteAsJsonFileName, tempTxtFileName);
            ContentCopyAndPasteAsJsonCase2(tempTxtFileName);
            result = FileReader.CompareRtfFiles(
                Path.Combine(testFilesFolderPath, tempTxtFileName),
                Path.Combine(testFilesFolderPath, pasteAsJsonResultFile),
                compareFormatting: true);
            Assert.IsTrue(result.IsConsistent, "Paste as Json using shortcut failed.");

            // Copy some text(same as in the previous step or different.If nothing is coppied between steps, previously pasted JSON text will be picked up from clipboard and converted again to nested JSON).
            // Open Advanced Paste window using hotkey, press Ctrl + 3 and confirm that pasted text is converted to markdown
            DeleteAndCopyFile(pasteAsJsonFileName, tempTxtFileName);
            ContentCopyAndPasteAsJsonCase3(tempTxtFileName);
            result = FileReader.CompareRtfFiles(
                Path.Combine(testFilesFolderPath, tempTxtFileName),
                Path.Combine(testFilesFolderPath, pasteAsJsonResultFile),
                compareFormatting: true);
            Assert.IsTrue(result.IsConsistent, "Paste as Json using shortcut failed.");
        }

        /*
         * Clipboard History
           - [] Open Settings and Enable clipboard history (if not enabled already). Open Advanced Paste window with hotkey, click Clipboard history and try deleting some entry. Check OS clipboard history (Win+V), and confirm that the same entry no longer exist.
           - [] Open Advanced Paste window with hotkey, click Clipboard history, and click any entry (but first). Observe that entry is put on top of clipboard history. Check OS clipboard history (Win+V), and confirm that the same entry is on top of the clipboard.
           - [] Open Settings and Disable clipboard history. Open Advanced Paste window with hotkey and observe that Clipboard history button is disabled.
         * Disable Advanced Paste, try different Advanced Paste hotkeys and confirm that it's disabled and nothing happens.
         */
        private void TestCaseClipboardHistory()
        {
        }

        private void ContentCopyAndPasteDirectly(string fileName, bool isRTF = false)
        {
            string tempFile = Path.Combine(testFilesFolderPath, fileName);

            Process process = Process.Start(isRTF ? wordpadPath : "notepad.exe", tempFile);
            if (process == null)
            {
                throw new InvalidOperationException($"Failed to start {(isRTF ? "WordPad" : "Notepad")}.");
            }

            string windowTitle = Path.GetFileName(tempFile) + (isRTF ? " - WordPad" : " - Notepad");

            Thread.Sleep(500);

            // Replace SetForegroundWindow with the improved function
            var window = this.Find<Window>(windowTitle, global: true);

            if (window == null)
            {
                throw new InvalidOperationException($"Failed to set focus to {(isRTF ? "WordPad" : "Notepad")} window.");
            }

            window.Click();
            Thread.Sleep(200);

            this.SendKeys(Key.LCtrl, Key.A);
            Thread.Sleep(300);
            this.SendKeys(Key.LCtrl, Key.C);
            Thread.Sleep(300);
            this.SendKeys(Key.Delete);
            Thread.Sleep(300);
            this.SendKeys(Key.LCtrl, Key.V);
            Thread.Sleep(300);
            this.SendKeys(Key.Backspace);
            Thread.Sleep(300);
            this.SendKeys(Key.LCtrl, Key.S);
            Thread.Sleep(300);

            process.Kill(true);
        }

        private void ContentCopyAndPasteWithShortcutThenPasteAgain(string fileName, bool isRTF = false, params Key[] keys)
        {
            string tempFile = Path.Combine(testFilesFolderPath, fileName);

            Process process = Process.Start(isRTF ? wordpadPath : "notepad.exe", tempFile);
            if (process == null)
            {
                throw new InvalidOperationException($"Failed to start {(isRTF ? "WordPad" : "Notepad")}.");
            }

            string windowTitle = Path.GetFileName(tempFile) + (isRTF ? " - WordPad" : " - Notepad");

            Thread.Sleep(500);

            // Replace SetForegroundWindow with the improved function
            var window = this.Find<Window>(windowTitle, global: true);

            if (window == null)
            {
                throw new InvalidOperationException($"Failed to set focus to {(isRTF ? "WordPad" : "Notepad")} window.");
            }

            window.Click();
            Thread.Sleep(200);

            this.SendKeys(Key.LCtrl, Key.A);
            Thread.Sleep(300);
            this.SendKeys(Key.LCtrl, Key.C);
            Thread.Sleep(300);
            this.SendKeys(Key.Delete);
            Thread.Sleep(300);
            this.SendKeys(keys);
            Thread.Sleep(300);
            this.SendKeys(Key.LCtrl, Key.V);
            Thread.Sleep(300);
            this.SendKeys(Key.LCtrl, Key.S);
            Thread.Sleep(300);

            process.Kill(true);
        }

        private void ContentCopyAndPasteCase3(string fileName, bool isRTF = false)
        {
            // Copy some rich text again.
            // Open Advanced Paste window using hotkey, click Paste as Plain Text button and confirm that plain text without any formatting is pasted.
            string tempFile = Path.Combine(testFilesFolderPath, fileName);

            Process process = Process.Start(isRTF ? wordpadPath : "notepad.exe", tempFile);
            if (process == null)
            {
                throw new InvalidOperationException($"Failed to start {(isRTF ? "WordPad" : "Notepad")}.");
            }

            string windowTitle = Path.GetFileName(tempFile) + (isRTF ? " - WordPad" : " - Notepad");

            Thread.Sleep(500);

            // Replace SetForegroundWindow with the improved function
            var window = this.Find<Window>(windowTitle, global: true);

            if (window == null)
            {
                throw new InvalidOperationException($"Failed to set focus to {(isRTF ? "WordPad" : "Notepad")} window.");
            }

            window.Click();
            Thread.Sleep(200);

            this.SendKeys(Key.LCtrl, Key.A);
            Thread.Sleep(300);
            this.SendKeys(Key.LCtrl, Key.C);
            Thread.Sleep(300);
            this.SendKeys(Key.Delete);
            Thread.Sleep(300);

            // Open Advanced Paste window using hotkey
            this.SendKeys(Key.Win, Key.Shift, Key.V);
            Thread.Sleep(300);

            // Click Paste as Plain Text button and confirm that plain text without any formatting is pasted.
            var apWind = this.Find<Window>("Advanced Paste", global: true);
            apWind.Find<TextBlock>("Paste as plain text").Click();

            this.SendKeys(Key.LCtrl, Key.S);
            Thread.Sleep(300);

            process.Kill(true);
        }

        private void ContentCopyAndPasteCase4(string fileName, bool isRTF = false)
        {
            // Copy some rich text again.
            // Open Advanced Paste window using hotkey, press Ctrl + 1 and confirm that plain text without any formatting is pasted.
            string tempFile = Path.Combine(testFilesFolderPath, fileName);

            Process process = Process.Start(isRTF ? wordpadPath : "notepad.exe", tempFile);
            if (process == null)
            {
                throw new InvalidOperationException($"Failed to start {(isRTF ? "WordPad" : "Notepad")}.");
            }

            string windowTitle = Path.GetFileName(tempFile) + (isRTF ? " - WordPad" : " - Notepad");

            Thread.Sleep(500);

            // Replace SetForegroundWindow with the improved function
            var window = this.Find<Window>(windowTitle, global: true);

            if (window == null)
            {
                throw new InvalidOperationException($"Failed to set focus to {(isRTF ? "WordPad" : "Notepad")} window.");
            }

            window.Click();
            Thread.Sleep(200);

            this.SendKeys(Key.LCtrl, Key.A);
            Thread.Sleep(300);
            this.SendKeys(Key.LCtrl, Key.C);
            Thread.Sleep(300);
            this.SendKeys(Key.Delete);
            Thread.Sleep(300);

            // Open Advanced Paste window using hotkey
            this.SendKeys(Key.Win, Key.Shift, Key.V);
            Thread.Sleep(300);

            // press Ctrl + 1 and confirm that plain text without any formatting is pasted.
            this.SendKeys(Key.LCtrl, Key.Num1);
            Thread.Sleep(300);

            this.SendKeys(Key.LCtrl, Key.S);
            Thread.Sleep(300);

            process.Kill(true);
        }

        private void ContentCopyAndPasteAsMarkdownCase1(string fileName, bool isRTF = false)
        {
            // Copy some rich text again.
            // Open Advanced Paste window using hotkey, press Ctrl + 1 and confirm that plain text without any formatting is pasted.
            string tempFile = Path.Combine(testFilesFolderPath, fileName);

            Process process = Process.Start(isRTF ? wordpadPath : "notepad.exe", tempFile);
            if (process == null)
            {
                throw new InvalidOperationException($"Failed to start {(isRTF ? "WordPad" : "Notepad")}.");
            }

            string windowTitle = Path.GetFileName(tempFile) + (isRTF ? " - WordPad" : " - Notepad");

            Thread.Sleep(500);

            // Replace SetForegroundWindow with the improved function
            var window = this.Find<Window>(windowTitle, global: true);

            if (window == null)
            {
                throw new InvalidOperationException($"Failed to set focus to {(isRTF ? "WordPad" : "Notepad")} window.");
            }

            window.Click();
            Thread.Sleep(200);

            this.SendKeys(Key.LCtrl, Key.A);
            Thread.Sleep(300);
            this.SendKeys(Key.LCtrl, Key.C);
            Thread.Sleep(300);
            this.SendKeys(Key.Delete);
            Thread.Sleep(300);

            this.SendKeys(Key.Win, Key.LCtrl, Key.Alt, Key.M);
            Thread.Sleep(300);

            this.SendKeys(Key.LCtrl, Key.S);
            Thread.Sleep(300);

            process.Kill(true);
        }

        private void ContentCopyAndPasteAsMarkdownCase2(string fileName, bool isRTF = false)
        {
            string tempFile = Path.Combine(testFilesFolderPath, fileName);

            Process process = Process.Start(isRTF ? wordpadPath : "notepad.exe", tempFile);
            if (process == null)
            {
                throw new InvalidOperationException($"Failed to start {(isRTF ? "WordPad" : "Notepad")}.");
            }

            string windowTitle = Path.GetFileName(tempFile) + (isRTF ? " - WordPad" : " - Notepad");

            Thread.Sleep(500);

            // Replace SetForegroundWindow with the improved function
            var window = this.Find<Window>(windowTitle, global: true);

            if (window == null)
            {
                throw new InvalidOperationException($"Failed to set focus to {(isRTF ? "WordPad" : "Notepad")} window.");
            }

            window.Click();
            Thread.Sleep(200);

            this.SendKeys(Key.LCtrl, Key.A);
            Thread.Sleep(300);
            this.SendKeys(Key.LCtrl, Key.C);
            Thread.Sleep(300);
            this.SendKeys(Key.Delete);
            Thread.Sleep(300);

            // Open Advanced Paste window using hotkey
            this.SendKeys(Key.Win, Key.Shift, Key.V);
            Thread.Sleep(300);

            // click Paste as markdown button and confirm that pasted text is converted to markdown
            var apWind = this.Find<Window>("Advanced Paste", global: true);
            apWind.Find<TextBlock>("Paste as markdown").Click();

            this.SendKeys(Key.LCtrl, Key.S);
            Thread.Sleep(300);

            process.Kill(true);
        }

        private void ContentCopyAndPasteAsMarkdownCase3(string fileName, bool isRTF = false)
        {
            string tempFile = Path.Combine(testFilesFolderPath, fileName);

            Process process = Process.Start(isRTF ? wordpadPath : "notepad.exe", tempFile);
            if (process == null)
            {
                throw new InvalidOperationException($"Failed to start {(isRTF ? "WordPad" : "Notepad")}.");
            }

            string windowTitle = Path.GetFileName(tempFile) + (isRTF ? " - WordPad" : " - Notepad");

            Thread.Sleep(500);

            // Replace SetForegroundWindow with the improved function
            var window = this.Find<Window>(windowTitle, global: true);

            if (window == null)
            {
                throw new InvalidOperationException($"Failed to set focus to {(isRTF ? "WordPad" : "Notepad")} window.");
            }

            window.Click();
            Thread.Sleep(200);

            this.SendKeys(Key.LCtrl, Key.A);
            Thread.Sleep(300);
            this.SendKeys(Key.LCtrl, Key.C);
            Thread.Sleep(300);
            this.SendKeys(Key.Delete);
            Thread.Sleep(300);

            // Open Advanced Paste window using hotkey
            this.SendKeys(Key.Win, Key.Shift, Key.V);
            Thread.Sleep(300);

            this.SendKeys(Key.LCtrl, Key.Num2);
            Thread.Sleep(300);

            this.SendKeys(Key.LCtrl, Key.S);
            Thread.Sleep(300);

            process.Kill(true);
        }

        private void ContentCopyAndPasteAsJsonCase1(string fileName, bool isRTF = false)
        {
            // Copy some rich text again.
            // Open Advanced Paste window using hotkey, press Ctrl + 1 and confirm that plain text without any formatting is pasted.
            string tempFile = Path.Combine(testFilesFolderPath, fileName);

            Process process = Process.Start(isRTF ? wordpadPath : "notepad.exe", tempFile);
            if (process == null)
            {
                throw new InvalidOperationException($"Failed to start {(isRTF ? "WordPad" : "Notepad")}.");
            }

            string windowTitle = Path.GetFileName(tempFile) + (isRTF ? " - WordPad" : " - Notepad");

            Thread.Sleep(500);

            // Replace SetForegroundWindow with the improved function
            var window = this.Find<Window>(windowTitle, global: true);

            if (window == null)
            {
                throw new InvalidOperationException($"Failed to set focus to {(isRTF ? "WordPad" : "Notepad")} window.");
            }

            window.Click();
            Thread.Sleep(200);

            this.SendKeys(Key.LCtrl, Key.A);
            Thread.Sleep(300);
            this.SendKeys(Key.LCtrl, Key.C);
            Thread.Sleep(300);
            this.SendKeys(Key.Delete);
            Thread.Sleep(300);

            this.SendKeys(Key.Win, Key.LCtrl, Key.Alt, Key.J);
            Thread.Sleep(300);

            this.SendKeys(Key.LCtrl, Key.S);
            Thread.Sleep(300);

            process.Kill(true);
        }

        private void ContentCopyAndPasteAsJsonCase2(string fileName, bool isRTF = false)
        {
            string tempFile = Path.Combine(testFilesFolderPath, fileName);

            Process process = Process.Start(isRTF ? wordpadPath : "notepad.exe", tempFile);
            if (process == null)
            {
                throw new InvalidOperationException($"Failed to start {(isRTF ? "WordPad" : "Notepad")}.");
            }

            string windowTitle = Path.GetFileName(tempFile) + (isRTF ? " - WordPad" : " - Notepad");

            Thread.Sleep(500);

            // Replace SetForegroundWindow with the improved function
            var window = this.Find<Window>(windowTitle, global: true);

            if (window == null)
            {
                throw new InvalidOperationException($"Failed to set focus to {(isRTF ? "WordPad" : "Notepad")} window.");
            }

            window.Click();
            Thread.Sleep(200);

            this.SendKeys(Key.LCtrl, Key.A);
            Thread.Sleep(300);
            this.SendKeys(Key.LCtrl, Key.C);
            Thread.Sleep(300);
            this.SendKeys(Key.Delete);
            Thread.Sleep(300);

            // Open Advanced Paste window using hotkey
            this.SendKeys(Key.Win, Key.Shift, Key.V);
            Thread.Sleep(300);

            // click Paste as markdown button and confirm that pasted text is converted to markdown
            var apWind = this.Find<Window>("Advanced Paste", global: true);
            apWind.Find<TextBlock>("Paste as JSON").Click();

            this.SendKeys(Key.LCtrl, Key.S);
            Thread.Sleep(300);

            process.Kill(true);
        }

        private void ContentCopyAndPasteAsJsonCase3(string fileName, bool isRTF = false)
        {
            string tempFile = Path.Combine(testFilesFolderPath, fileName);

            Process process = Process.Start(isRTF ? wordpadPath : "notepad.exe", tempFile);
            if (process == null)
            {
                throw new InvalidOperationException($"Failed to start {(isRTF ? "WordPad" : "Notepad")}.");
            }

            string windowTitle = Path.GetFileName(tempFile) + (isRTF ? " - WordPad" : " - Notepad");

            Thread.Sleep(500);

            // Replace SetForegroundWindow with the improved function
            var window = this.Find<Window>(windowTitle, global: true);

            if (window == null)
            {
                throw new InvalidOperationException($"Failed to set focus to {(isRTF ? "WordPad" : "Notepad")} window.");
            }

            window.Click();
            Thread.Sleep(200);

            this.SendKeys(Key.LCtrl, Key.A);
            Thread.Sleep(300);
            this.SendKeys(Key.LCtrl, Key.C);
            Thread.Sleep(300);
            this.SendKeys(Key.Delete);
            Thread.Sleep(300);

            // Open Advanced Paste window using hotkey
            this.SendKeys(Key.Win, Key.Shift, Key.V);
            Thread.Sleep(300);

            this.SendKeys(Key.LCtrl, Key.Num3);
            Thread.Sleep(300);

            this.SendKeys(Key.LCtrl, Key.S);
            Thread.Sleep(300);

            process.Kill(true);
        }

        private string DeleteAndCopyFile(string sourceFileName, string destinationFileName)
        {
            string sourcePath = Path.Combine(testFilesFolderPath, sourceFileName);
            string destinationPath = Path.Combine(testFilesFolderPath, destinationFileName);

            // Check if source file exists
            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException($"Source file not found: {sourcePath}");
            }

            // Delete destination file if it exists
            if (File.Exists(destinationPath))
            {
                try
                {
                    File.Delete(destinationPath);
                }
                catch (IOException ex)
                {
                    throw new IOException($"Failed to delete file {destinationPath}. The file may be in use: {ex.Message}", ex);
                }
            }

            // Copy the source file to the destination
            try
            {
                File.Copy(sourcePath, destinationPath);
            }
            catch (IOException ex)
            {
                throw new IOException($"Failed to copy file from {sourcePath} to {destinationPath}: {ex.Message}", ex);
            }

            return destinationPath;
        }
    }
}
