// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
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

        private bool _notepadSettingsChanged;

        // Static constructor - runs before any instance is created
        static AdvancedPasteUITest()
        {
            // Using the predefined settings.
            // paste as plain text: win + ctrl + alt + o
            // paste as markdown text: win + ctrl + alt + m
            // paste as json text: win + ctrl + alt + j
            CopySettingsFileBeforeTests();
        }

        public AdvancedPasteUITest()
            : base(PowerToysModule.PowerToysSettings, size: WindowSize.Large_Vertical)
        {
            Type currentTestType = typeof(AdvancedPasteUITest);
            string? dirName = Path.GetDirectoryName(currentTestType.Assembly.Location);
            Assert.IsNotNull(dirName, "Failed to get directory name of the current test assembly.");

            string testFilesFolder = Path.Combine(dirName, "TestFiles");
            Assert.IsTrue(Directory.Exists(testFilesFolder), $"Test files directory not found at: {testFilesFolder}");

            testFilesFolderPath = testFilesFolder;

            // ignore the notepad settings in pipeline
            _notepadSettingsChanged = true;
        }

        [TestInitialize]
        public void TestInitialize()
        {
            Session.CloseMainWindow();
            SendKeys(Key.Win, Key.M);
        }

        [TestMethod]
        [TestCategory("AdvancedPasteUITest")]
        [TestCategory("PasteAsPlainText")]
        [Ignore("Temporarily disabled due to wordpad.exe is missing in pipeline.")]
        public void TestCasePasteAsPlainText()
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
            ContentCopyAndPasteWithShortcutThenPasteAgain(tempRTFFileName, isRTF: true);
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

        [TestMethod]
        [TestCategory("AdvancedPasteUITest")]
        [TestCategory("PasteAsMarkdownCase1")]
        public void TestCasePasteAsMarkdownCase1()
        {
            if (_notepadSettingsChanged == false)
            {
                ChangeNotePadSettings();
            }

            // Copy some text(e.g.some HTML text - convertible to Markdown)
            // Paste the text using set hotkey and confirm that pasted text is converted to markdown
            DeleteAndCopyFile(pasteAsMarkdownSrcFile, tempTxtFileName);
            ContentCopyAndPasteAsMarkdownCase1(tempTxtFileName);
            var result = FileReader.CompareRtfFiles(
                Path.Combine(testFilesFolderPath, tempTxtFileName),
                Path.Combine(testFilesFolderPath, pasteAsMarkdownResultFile),
                compareFormatting: true);
            Assert.IsTrue(result.IsConsistent, "Paste as markdown using shortcut failed.");
        }

        [TestMethod]
        [TestCategory("AdvancedPasteUITest")]
        [TestCategory("PasteAsMarkdownCase2")]
        public void TestCasePasteAsMarkdownCase2()
        {
            if (_notepadSettingsChanged == false)
            {
                ChangeNotePadSettings();
            }

            // Copy some text(same as in the previous step or different.If nothing is coppied between steps, previously pasted Markdown text will be picked up from clipboard and converted again to nested Markdown).
            // Open Advanced Paste window using hotkey, click Paste as markdown button and confirm that pasted text is converted to markdown
            DeleteAndCopyFile(pasteAsMarkdownSrcFile, tempTxtFileName);
            ContentCopyAndPasteAsMarkdownCase2(tempTxtFileName);
            var result = FileReader.CompareRtfFiles(
                Path.Combine(testFilesFolderPath, tempTxtFileName),
                Path.Combine(testFilesFolderPath, pasteAsMarkdownResultFile),
                compareFormatting: true);
            Assert.IsTrue(result.IsConsistent, "Paste as markdown using shortcut failed.");
        }

        [TestMethod]
        [TestCategory("AdvancedPasteUITest")]
        [TestCategory("PasteAsMarkdownCase3")]
        public void TestCasePasteAsMarkdownCase3()
        {
            if (_notepadSettingsChanged == false)
            {
                ChangeNotePadSettings();
            }

            // Copy some text(same as in the previous step or different.If nothing is coppied between steps, previously pasted Markdown text will be picked up from clipboard and converted again to nested Markdown).
            // Open Advanced Paste window using hotkey, press Ctrl + 2 and confirm that pasted text is converted to markdown
            DeleteAndCopyFile(pasteAsMarkdownSrcFile, tempTxtFileName);
            ContentCopyAndPasteAsMarkdownCase3(tempTxtFileName);
            var result = FileReader.CompareRtfFiles(
                Path.Combine(testFilesFolderPath, tempTxtFileName),
                Path.Combine(testFilesFolderPath, pasteAsMarkdownResultFile),
                compareFormatting: true);
            Assert.IsTrue(result.IsConsistent, "Paste as markdown using shortcut failed.");
        }

        [TestMethod]
        [TestCategory("AdvancedPasteUITest")]
        [TestCategory("PasteAsJSONCase1")]
        public void TestCasePasteAsJSONCase1()
        {
            if (_notepadSettingsChanged == false)
            {
                ChangeNotePadSettings();
            }

            // Copy some XML or CSV text(or any other text, it will be converted to simple JSON object)
            // Paste the text using set hotkey and confirm that pasted text is converted to JSON
            DeleteAndCopyFile(pasteAsJsonFileName, tempTxtFileName);
            ContentCopyAndPasteAsJsonCase1(tempTxtFileName);
            var result = FileReader.CompareRtfFiles(
                Path.Combine(testFilesFolderPath, tempTxtFileName),
                Path.Combine(testFilesFolderPath, pasteAsJsonResultFile),
                compareFormatting: true);
            Assert.IsTrue(result.IsConsistent, "Paste as Json using shortcut failed.");
        }

        [TestMethod]
        [TestCategory("AdvancedPasteUITest")]
        [TestCategory("PasteAsJSONCase2")]
        public void TestCasePasteAsJSONCase2()
        {
            if (_notepadSettingsChanged == false)
            {
                ChangeNotePadSettings();
            }

            // Copy some text(same as in the previous step or different.If nothing is coppied between steps, previously pasted JSON text will be picked up from clipboard and converted again to nested JSON).
            // Open Advanced Paste window using hotkey, click Paste as markdown button and confirm that pasted text is converted to markdown
            DeleteAndCopyFile(pasteAsJsonFileName, tempTxtFileName);
            ContentCopyAndPasteAsJsonCase2(tempTxtFileName);
            var result = FileReader.CompareRtfFiles(
                Path.Combine(testFilesFolderPath, tempTxtFileName),
                Path.Combine(testFilesFolderPath, pasteAsJsonResultFile),
                compareFormatting: true);
            Assert.IsTrue(result.IsConsistent, "Paste as Json using shortcut failed.");
        }

        [TestMethod]
        [TestCategory("AdvancedPasteUITest")]
        [TestCategory("PasteAsJSONCase3")]
        public void TestCasePasteAsJSONCase3()
        {
            if (_notepadSettingsChanged == false)
            {
                ChangeNotePadSettings();
            }

            // Copy some text(same as in the previous step or different.If nothing is coppied between steps, previously pasted JSON text will be picked up from clipboard and converted again to nested JSON).
            // Open Advanced Paste window using hotkey, press Ctrl + 3 and confirm that pasted text is converted to markdown
            DeleteAndCopyFile(pasteAsJsonFileName, tempTxtFileName);
            ContentCopyAndPasteAsJsonCase3(tempTxtFileName);
            var result = FileReader.CompareRtfFiles(
                Path.Combine(testFilesFolderPath, tempTxtFileName),
                Path.Combine(testFilesFolderPath, pasteAsJsonResultFile),
                compareFormatting: true);
            Assert.IsTrue(result.IsConsistent, "Paste as Json using shortcut failed.");
        }

        /*
         * Clipboard History
           - [x] Open Settings and Enable clipboard history (if not enabled already). Open Advanced Paste window with hotkey, click Clipboard history and try deleting some entry. Check OS clipboard history (Win+V), and confirm that the same entry no longer exist.
           - [x] Open Advanced Paste window with hotkey, click Clipboard history, and click any entry (but first). Observe that entry is put on top of clipboard history. Check OS clipboard history (Win+V), and confirm that the same entry is on top of the clipboard.
           - [x] Open Settings and Disable clipboard history. Open Advanced Paste window with hotkey and observe that Clipboard history button is disabled.
         * Disable Advanced Paste, try different Advanced Paste hotkeys and confirm that it's disabled and nothing happens.
         */
        [TestMethod]
        [TestCategory("AdvancedPasteUITest")]
        [TestCategory("TestCaseClipboardHistoryDeleteTest")]
        public void TestCaseClipboardHistoryDeleteTest()
        {
            RestartScopeExe();
            Thread.Sleep(1500);

            // Find the PowerToys Settings window
            var settingsWindow = Find<Window>("PowerToys Settings", global: true);
            Assert.IsNotNull(settingsWindow, "Failed to open PowerToys Settings window");

            if (FindAll<NavigationViewItem>("Advanced Paste").Count == 0)
            {
                // Expand Advanced list-group if needed
                Find<NavigationViewItem>("System Tools").Click();
            }

            Find<NavigationViewItem>("Advanced Paste").Click();

            Find<ToggleSwitch>("Clipboard history").Toggle(true);

            Session.CloseMainWindow();

            // clear system clipboard
            ClearSystemClipboardHistory();

            // set test content to clipboard
            const string textForTesting = "Test text";
            SetClipboardTextInSTAMode(textForTesting);

            // Open Advanced Paste window with hotkey, click Clipboard history and try deleting some entry.
            this.SendKeys(Key.Win, Key.Shift, Key.V);
            Thread.Sleep(1500);

            var apWind = this.Find<Window>("Advanced Paste", global: true);
            apWind.Find<PowerToys.UITest.Button>("Clipboard history").Click();

            var textGroup = apWind.Find<Group>(textForTesting);
            Assert.IsNotNull(textGroup, "Cannot find the test string from advanced paste clipboard history.");

            textGroup.Find<PowerToys.UITest.Button>("More options").Click();
            apWind.Find<TextBlock>("Delete").Click();

            // Check OS clipboard history (Win+V), and confirm that the same entry no longer exist.
            this.SendKeys(Key.Win, Key.V);

            Thread.Sleep(1500);

            var clipboardWindow = this.Find<Window>("Windows Input Experience", global: true);
            Assert.IsNotNull(clipboardWindow, "Cannot find system clipboard window.");

            var nothingText = clipboardWindow.Find<Group>("Nothing here, You'll see your clipboard history here once you've copied something.");
            Assert.IsNotNull(nothingText, "System clipboard is not empty, which should be yes.");
        }

        [TestMethod]
        [TestCategory("AdvancedPasteUITest")]
        [TestCategory("TestCaseClipboardHistorySelectTest")]
        public void TestCaseClipboardHistorySelectTest()
        {
            RestartScopeExe();
            Thread.Sleep(1500);

            // Find the PowerToys Settings window
            var settingsWindow = Find<Window>("PowerToys Settings", global: true);
            Assert.IsNotNull(settingsWindow, "Failed to open PowerToys Settings window");

            if (FindAll<NavigationViewItem>("Advanced Paste").Count == 0)
            {
                // Expand Advanced list-group if needed
                Find<NavigationViewItem>("System Tools").Click();
            }

            Find<NavigationViewItem>("Advanced Paste").Click();

            Find<ToggleSwitch>("Clipboard history").Toggle(true);

            Session.CloseMainWindow();

            // clear system clipboard
            ClearSystemClipboardHistory();

            // set test content to clipboard
            string[] textForTesting = { "Test text1", "Test text2", "Test text3", "Test text4", "Test text5", "Test text6", };
            foreach (var str in textForTesting)
            {
                SetClipboardTextInSTAMode(str);
                Thread.Sleep(1000);
            }

            // Open Advanced Paste window with hotkey
            this.SendKeys(Key.Win, Key.Shift, Key.V);
            Thread.Sleep(1500);

            var apWind = this.Find<Window>("Advanced Paste", global: true);
            apWind.Find<PowerToys.UITest.Button>("Clipboard history").Click();

            // click the 3rd item
            var textGroup = apWind.Find<Group>(textForTesting[0]);
            Assert.IsNotNull(textGroup, "Cannot find the test string from advanced paste clipboard history.");
            textGroup.Click();

            // Check OS clipboard history (Win+V)
            this.SendKeys(Key.Win, Key.V);

            Thread.Sleep(1500);

            var clipboardWindow = this.Find<Window>("Windows Input Experience", global: true);
            Assert.IsNotNull(clipboardWindow, "Cannot find system clipboard window.");

            var txtFound = clipboardWindow.Find<Element>(textForTesting[0]);
            Assert.IsNotNull(txtFound, "Cannot find textblock");
        }

        // [x] Open Settings and Disable clipboard history.Open Advanced Paste window with hotkey and observe that Clipboard history button is disabled.
        [TestMethod]
        [TestCategory("AdvancedPasteUITest")]
        [TestCategory("TestCaseClipboardHistoryDisableTest")]
        public void TestCaseClipboardHistoryDisableTest()
        {
            RestartScopeExe();
            Thread.Sleep(1500);

            // Find the PowerToys Settings window
            var settingsWindow = Find<Window>("PowerToys Settings", global: true);
            Assert.IsNotNull(settingsWindow, "Failed to open PowerToys Settings window");

            if (FindAll<NavigationViewItem>("Advanced Paste").Count == 0)
            {
                // Expand Advanced list-group if needed
                Find<NavigationViewItem>("System Tools").Click();
            }

            Find<NavigationViewItem>("Advanced Paste").Click();

            Find<ToggleSwitch>("Clipboard history").Toggle(false);

            Session.CloseMainWindow();

            // set test content to clipboard
            const string textForTesting = "Test text";
            SetClipboardTextInSTAMode(textForTesting);

            // Open Advanced Paste window with hotkey, click Clipboard history and try deleting some entry.
            this.SendKeys(Key.Win, Key.Shift, Key.V);
            Thread.Sleep(1500);

            var apWind = this.Find<Window>("Advanced Paste", global: true);

            // Click the button (which should still exist but be disabled)
            apWind.Find<PowerToys.UITest.Button>("Clipboard history").Click();

            // Verify that the clipboard content doesn't appear
            // Use a short timeout to avoid a long wait when the element doesn't exist
            Assert.IsFalse(
                Has<Group>(textForTesting),
                "Clipboard content should not appear when clipboard history is disabled");
        }

        // Disable Advanced Paste, try different Advanced Paste hotkeys and confirm that it's disabled and nothing happens.
        [TestMethod]
        [TestCategory("AdvancedPasteUITest")]
        [TestCategory("TestCaseDisableAdvancedPaste")]
        public void TestCaseDisableAdvancedPaste()
        {
            RestartScopeExe();
            Thread.Sleep(1500);

            // Find the PowerToys Settings window
            var settingsWindow = Find<Window>("PowerToys Settings", global: true);
            Assert.IsNotNull(settingsWindow, "Failed to open PowerToys Settings window");

            if (FindAll<NavigationViewItem>("Advanced Paste").Count == 0)
            {
                // Expand System Tools if needed
                Find<NavigationViewItem>("System Tools").Click();
            }

            Find<NavigationViewItem>("Advanced Paste").Click();

            // Disable Advanced Paste module
            var moduleToggle = Find<ToggleSwitch>("Enable Advanced Paste");
            moduleToggle.Toggle(false);

            Session.CloseMainWindow();

            // Prepare some text to test with
            const string textForTesting = "Test text for disabled module";
            SetClipboardTextInSTAMode(textForTesting);

            // Try main Advanced Paste hotkey
            this.SendKeys(Key.Win, Key.Shift, Key.V);
            Thread.Sleep(500);

            // Verify Advanced Paste window does not appear
            Assert.IsFalse(
                Has<Window>("Advanced Paste", global: true),
                "Advanced Paste window should not appear when the module is disabled");

            // Re-enable Advanced Paste for other tests
            RestartScopeExe();
            Thread.Sleep(1500);

            settingsWindow = Find<Window>("PowerToys Settings", global: true);

            if (FindAll<NavigationViewItem>("Advanced Paste").Count == 0)
            {
                Find<NavigationViewItem>("System Tools").Click();
            }

            Find<NavigationViewItem>("Advanced Paste").Click();
            Find<ToggleSwitch>("Enable Advanced Paste").Toggle(true);

            Session.CloseMainWindow();
        }

        private void ClearSystemClipboardHistory()
        {
            this.SendKeys(Key.Win, Key.V);

            Thread.Sleep(1500);

            var clipboardWindow = this.Find<Window>("Windows Input Experience", global: true);
            Assert.IsNotNull(clipboardWindow, "Cannot find system clipboard window.");

            clipboardWindow.Find<PowerToys.UITest.Button>("Clear all except pinned items").Click();
        }

        private void SetClipboardTextInSTAMode(string text)
        {
            var thread = new Thread(() =>
            {
                System.Windows.Forms.Clipboard.SetText(text);
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
        }

        private void ContentCopyAndPasteDirectly(string fileName, bool isRTF = false)
        {
            string tempFile = Path.Combine(testFilesFolderPath, fileName);

            Process process = Process.Start(isRTF ? wordpadPath : "notepad.exe", tempFile);
            if (process == null)
            {
                throw new InvalidOperationException($"Failed to start {(isRTF ? "WordPad" : "Notepad")}.");
            }

            Thread.Sleep(15000);

            var window = FindWindowWithFlexibleTitle(Path.GetFileName(tempFile), isRTF);

            window.Click();
            Thread.Sleep(1000);

            this.SendKeys(Key.LCtrl, Key.A);
            Thread.Sleep(1000);
            this.SendKeys(Key.LCtrl, Key.C);
            Thread.Sleep(1000);
            this.SendKeys(Key.Delete);
            Thread.Sleep(1000);
            this.SendKeys(Key.LCtrl, Key.V);
            Thread.Sleep(1000);
            this.SendKeys(Key.Backspace);
            Thread.Sleep(1000);
            this.SendKeys(Key.LCtrl, Key.S);
            Thread.Sleep(1000);

            process.Kill(true);
        }

        private void ContentCopyAndPasteWithShortcutThenPasteAgain(string fileName, bool isRTF = false)
        {
            string tempFile = Path.Combine(testFilesFolderPath, fileName);

            Process process = Process.Start(isRTF ? wordpadPath : "notepad.exe", tempFile);
            if (process == null)
            {
                throw new InvalidOperationException($"Failed to start {(isRTF ? "WordPad" : "Notepad")}.");
            }

            Thread.Sleep(15000);

            var window = FindWindowWithFlexibleTitle(Path.GetFileName(tempFile), isRTF);

            window.Click();
            Thread.Sleep(1000);

            this.SendKeys(Key.LCtrl, Key.A);
            Thread.Sleep(1000);
            this.SendKeys(Key.LCtrl, Key.C);
            Thread.Sleep(1000);
            this.SendKeys(Key.Delete);
            Thread.Sleep(1000);
            this.SendKeys(Key.Win, Key.LCtrl, Key.Alt, Key.O);
            Thread.Sleep(1000);
            this.SendKeys(Key.LCtrl, Key.V);
            Thread.Sleep(1000);
            this.SendKeys(Key.LCtrl, Key.S);
            Thread.Sleep(1000);

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

            Thread.Sleep(15000);

            var window = FindWindowWithFlexibleTitle(Path.GetFileName(tempFile), isRTF);

            window.Click();
            Thread.Sleep(1000);

            this.SendKeys(Key.LCtrl, Key.A);
            Thread.Sleep(1000);
            this.SendKeys(Key.LCtrl, Key.C);
            Thread.Sleep(1000);
            this.SendKeys(Key.Delete);
            Thread.Sleep(1000);

            // Open Advanced Paste window using hotkey
            this.SendKeys(Key.Win, Key.Shift, Key.V);
            Thread.Sleep(15000);

            // Click Paste as Plain Text button and confirm that plain text without any formatting is pasted.
            var apWind = this.Find<Window>("Advanced Paste", global: true);
            apWind.Find<TextBlock>("Paste as plain text").Click();

            this.SendKeys(Key.LCtrl, Key.S);
            Thread.Sleep(1000);

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

            Thread.Sleep(15000);
            var window = FindWindowWithFlexibleTitle(Path.GetFileName(tempFile), isRTF);

            window.Click();
            Thread.Sleep(1000);

            this.SendKeys(Key.LCtrl, Key.A);
            Thread.Sleep(1000);
            this.SendKeys(Key.LCtrl, Key.C);
            Thread.Sleep(1000);
            this.SendKeys(Key.Delete);
            Thread.Sleep(1000);

            // Open Advanced Paste window using hotkey
            this.SendKeys(Key.Win, Key.Shift, Key.V);
            Thread.Sleep(1000);

            // press Ctrl + 1 and confirm that plain text without any formatting is pasted.
            this.SendKeys(Key.LCtrl, Key.Num1);
            Thread.Sleep(1000);

            this.SendKeys(Key.LCtrl, Key.S);
            Thread.Sleep(1000);

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

            Thread.Sleep(15000);

            var window = FindWindowWithFlexibleTitle(Path.GetFileName(tempFile), isRTF);

            window.Click();
            Thread.Sleep(1000);

            this.SendKeys(Key.LCtrl, Key.A);
            Thread.Sleep(1000);
            this.SendKeys(Key.LCtrl, Key.C);
            Thread.Sleep(1000);
            this.SendKeys(Key.Delete);
            Thread.Sleep(1000);

            this.SendKeys(Key.Win, Key.LCtrl, Key.Alt, Key.M);
            Thread.Sleep(1000);

            this.SendKeys(Key.LCtrl, Key.S);
            Thread.Sleep(1000);

            window.Close();
        }

        private void ContentCopyAndPasteAsMarkdownCase2(string fileName, bool isRTF = false)
        {
            string tempFile = Path.Combine(testFilesFolderPath, fileName);

            Process process = Process.Start(isRTF ? wordpadPath : "notepad.exe", tempFile);
            if (process == null)
            {
                throw new InvalidOperationException($"Failed to start {(isRTF ? "WordPad" : "Notepad")}.");
            }

            Thread.Sleep(15000);

            var window = FindWindowWithFlexibleTitle(Path.GetFileName(tempFile), isRTF);

            window.Click();
            Thread.Sleep(1000);

            this.SendKeys(Key.LCtrl, Key.A);
            Thread.Sleep(1000);
            this.SendKeys(Key.LCtrl, Key.C);
            Thread.Sleep(1000);
            this.SendKeys(Key.Delete);
            Thread.Sleep(1000);

            // Open Advanced Paste window using hotkey
            this.SendKeys(Key.Win, Key.Shift, Key.V);
            Thread.Sleep(15000);

            // click Paste as markdown button and confirm that pasted text is converted to markdown
            var apWind = this.Find<Window>("Advanced Paste", global: true);
            apWind.Find<TextBlock>("Paste as markdown").Click();

            this.SendKeys(Key.LCtrl, Key.S);
            Thread.Sleep(1000);

            window.Close();
        }

        private void ContentCopyAndPasteAsMarkdownCase3(string fileName, bool isRTF = false)
        {
            string tempFile = Path.Combine(testFilesFolderPath, fileName);

            Process process = Process.Start(isRTF ? wordpadPath : "notepad.exe", tempFile);
            if (process == null)
            {
                throw new InvalidOperationException($"Failed to start {(isRTF ? "WordPad" : "Notepad")}.");
            }

            Thread.Sleep(15000);
            var window = FindWindowWithFlexibleTitle(Path.GetFileName(tempFile), isRTF);

            window.Click();
            Thread.Sleep(1000);

            this.SendKeys(Key.LCtrl, Key.A);
            Thread.Sleep(1000);
            this.SendKeys(Key.LCtrl, Key.C);
            Thread.Sleep(1000);
            this.SendKeys(Key.Delete);
            Thread.Sleep(1000);

            // Open Advanced Paste window using hotkey
            this.SendKeys(Key.Win, Key.Shift, Key.V);
            Thread.Sleep(15000);

            this.SendKeys(Key.LCtrl, Key.Num2);
            Thread.Sleep(1000);

            this.SendKeys(Key.LCtrl, Key.S);
            Thread.Sleep(1000);

            window.Close();
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

            Thread.Sleep(15000);

            var window = FindWindowWithFlexibleTitle(Path.GetFileName(tempFile), isRTF);

            window.Click();
            Thread.Sleep(1000);

            this.SendKeys(Key.LCtrl, Key.A);
            Thread.Sleep(1000);
            this.SendKeys(Key.LCtrl, Key.C);
            Thread.Sleep(1000);
            this.SendKeys(Key.Delete);
            Thread.Sleep(1000);

            this.SendKeys(Key.Win, Key.LCtrl, Key.Alt, Key.J);
            Thread.Sleep(1000);

            this.SendKeys(Key.LCtrl, Key.S);
            Thread.Sleep(1000);

            window.Close();
        }

        private void ContentCopyAndPasteAsJsonCase2(string fileName, bool isRTF = false)
        {
            string tempFile = Path.Combine(testFilesFolderPath, fileName);

            Process process = Process.Start(isRTF ? wordpadPath : "notepad.exe", tempFile);
            if (process == null)
            {
                throw new InvalidOperationException($"Failed to start {(isRTF ? "WordPad" : "Notepad")}.");
            }

            Thread.Sleep(15000);

            var window = FindWindowWithFlexibleTitle(Path.GetFileName(tempFile), isRTF);

            window.Click();
            Thread.Sleep(1000);

            this.SendKeys(Key.LCtrl, Key.A);
            Thread.Sleep(1000);
            this.SendKeys(Key.LCtrl, Key.C);
            Thread.Sleep(1000);
            this.SendKeys(Key.Delete);
            Thread.Sleep(1000);

            // Open Advanced Paste window using hotkey
            this.SendKeys(Key.Win, Key.Shift, Key.V);
            Thread.Sleep(15000);

            // click Paste as markdown button and confirm that pasted text is converted to markdown
            var apWind = this.Find<Window>("Advanced Paste", global: true);
            apWind.Find<TextBlock>("Paste as JSON").Click();

            this.SendKeys(Key.LCtrl, Key.S);
            Thread.Sleep(1000);

            window.Close();
        }

        private void ContentCopyAndPasteAsJsonCase3(string fileName, bool isRTF = false)
        {
            string tempFile = Path.Combine(testFilesFolderPath, fileName);

            Process process = Process.Start(isRTF ? wordpadPath : "notepad.exe", tempFile);
            if (process == null)
            {
                throw new InvalidOperationException($"Failed to start {(isRTF ? "WordPad" : "Notepad")}.");
            }

            Thread.Sleep(15000);

            var window = FindWindowWithFlexibleTitle(Path.GetFileName(tempFile), isRTF);

            window.Click();
            Thread.Sleep(1000);

            this.SendKeys(Key.LCtrl, Key.A);
            Thread.Sleep(1000);
            this.SendKeys(Key.LCtrl, Key.C);
            Thread.Sleep(1000);
            this.SendKeys(Key.Delete);
            Thread.Sleep(1000);

            // Open Advanced Paste window using hotkey
            this.SendKeys(Key.Win, Key.Shift, Key.V);
            Thread.Sleep(15000);

            this.SendKeys(Key.LCtrl, Key.Num3);
            Thread.Sleep(1000);

            this.SendKeys(Key.LCtrl, Key.S);
            Thread.Sleep(1000);

            window.Close();
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

        private void ChangeNotePadSettings()
        {
            Process process = Process.Start("notepad.exe");
            if (process == null)
            {
                throw new InvalidOperationException($"Failed to start Notepad.exe");
            }

            Thread.Sleep(15000);

            var window = FindWindowWithFlexibleTitle("Untitled", false);

            window.Find<PowerToys.UITest.Button>("Settings").Click();
            var combobox = window.Find<PowerToys.UITest.ComboBox>("Opening files");
            combobox.SelectTxt("Open in a new window");

            window.Find<Group>("When Notepad starts").Click();

            window.Find<PowerToys.UITest.RadioButton>("Open a new window").Select();

            _notepadSettingsChanged = true;
            window.Close();
        }

        /// <summary>
        /// Finds a window with flexible title matching, trying multiple title variations
        /// </summary>
        /// <param name="baseTitle">The base title to search for</param>
        /// <param name="isRTF">Whether the window is a WordPad window</param>
        /// <returns>The found Window element or throws an exception if not found</returns>
        private Window FindWindowWithFlexibleTitle(string baseTitle, bool isRTF)
        {
            Window? window = null;
            string appType = isRTF ? "WordPad" : "Notepad";

            // Try different title variations
            string[] titleVariations = new string[]
            {
                baseTitle + (isRTF ? " - WordPad" : " - Notepad"),  // With suffix
                baseTitle,                                          // Without suffix
                Path.GetFileNameWithoutExtension(baseTitle) + (isRTF ? " - WordPad" : " - Notepad"),  // Without extension, with suffix
                Path.GetFileNameWithoutExtension(baseTitle),        // Without extension, without suffix
            };

            Exception? lastException = null;

            foreach (string title in titleVariations)
            {
                try
                {
                    window = this.Find<Window>(title, global: true);
                    if (window != null)
                    {
                        return window;
                    }
                }
                catch (Exception ex)
                {
                    // Save the exception, but continue trying other variations
                    lastException = ex;
                }
            }

            // If we couldn't find the window with any variation, throw an exception with details
            throw new InvalidOperationException(
                $"Failed to find {appType} window with title containing '{baseTitle}'. ");
        }

        private static void CopySettingsFileBeforeTests()
        {
            try
            {
                // Determine the assembly location and test files path
                string? assemblyLocation = Path.GetDirectoryName(typeof(AdvancedPasteUITest).Assembly.Location);
                if (assemblyLocation == null)
                {
                    Debug.WriteLine("ERROR: Failed to get assembly location");
                    return;
                }

                string testFilesFolder = Path.Combine(assemblyLocation, "TestFiles");
                if (!Directory.Exists(testFilesFolder))
                {
                    Debug.WriteLine($"ERROR: Test files directory not found at: {testFilesFolder}");
                    return;
                }

                // Settings file source path
                string settingsFileName = "settings.json";
                string sourceSettingsPath = Path.Combine(testFilesFolder, settingsFileName);

                // Make sure the source file exists
                if (!File.Exists(sourceSettingsPath))
                {
                    Debug.WriteLine($"ERROR: Settings file not found at: {sourceSettingsPath}");
                    return;
                }

                // Determine the target directory in %LOCALAPPDATA%
                string targetDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Microsoft",
                    "PowerToys",
                    "AdvancedPaste");

                // Create the directory if it doesn't exist
                if (!Directory.Exists(targetDirectory))
                {
                    Directory.CreateDirectory(targetDirectory);
                }

                string targetSettingsPath = Path.Combine(targetDirectory, settingsFileName);

                // Copy the file and overwrite if it exists
                File.Copy(sourceSettingsPath, targetSettingsPath, true);

                Debug.WriteLine($"Successfully copied settings file from {sourceSettingsPath} to {targetSettingsPath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR copying settings file: {ex.Message}");
            }
        }
    }
}
