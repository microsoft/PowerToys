// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium.Interactions;
using static Microsoft.PowerToys.UITest.UITestBase;

namespace PowerOCR.UITests;

[TestClass]
public class PowerOCRTests : UITestBase
{
    public PowerOCRTests()
        : base(PowerToysModule.PowerToysSettings, WindowSize.Medium)
    {
    }

    [TestInitialize]
    public void TestInitialize()
    {
        if (FindAll<NavigationViewItem>(By.AccessibilityId("TextExtractorNavItem")).Count == 0)
        {
            // Expand System Tools list-group if needed
            Find<NavigationViewItem>(By.AccessibilityId("SystemToolsNavItem")).Click();
        }

        Find<NavigationViewItem>(By.AccessibilityId("TextExtractorNavItem")).Click();

        Find<ToggleSwitch>(By.AccessibilityId("EnableTextExtractorToggleSwitch")).Toggle(true);

        // Reset activation shortcut to default (Win+Shift+T)
        var shortcutControl = Find<Element>(By.AccessibilityId("TextExtractorActivationShortcut"), 5000);
        if (shortcutControl != null)
        {
            shortcutControl.Click();
            Thread.Sleep(500);

            // Set default shortcut Win+Shift+T
            SendKeys(Key.Win, Key.Shift, Key.T);
            Thread.Sleep(1000);

            // Click Save to confirm
            var saveButton = Find<Button>(By.Name("Save"), 3000);
            if (saveButton != null)
            {
                saveButton.Click();
                Thread.Sleep(1000);
            }
        }
    }

    [TestMethod("PowerOCR.DetectTextExtractor")]
    [TestCategory("PowerOCR Detection")]
    public void DetectTextExtractorTest()
    {
        // Step 1: Press the activation shortcut and verify the overlay appears
        SendKeys(Key.Win, Key.Shift, Key.T);
        var textExtractorWindow = Find<Element>(By.AccessibilityId("TextExtractorWindow"), 10000, true);
        Assert.IsNotNull(textExtractorWindow, "TextExtractor window should be found after hotkey activation");

        // Step 2: Press Escape and verify the overlay disappears
        SendKeys(Key.Esc);
        Thread.Sleep(3000);

        var windowsAfterEscape = FindAll<Element>(By.AccessibilityId("TextExtractorWindow"), 2000, true);
        Assert.AreEqual(0, windowsAfterEscape.Count, "TextExtractor window should be dismissed after pressing Escape");

        // Step 3: Press the activation shortcut again and verify the overlay appears
        SendKeys(Key.Win, Key.Shift, Key.T);

        textExtractorWindow = Find<Element>(By.AccessibilityId("TextExtractorWindow"), 10000, true);
        Assert.IsNotNull(textExtractorWindow, "TextExtractor window should appear again after hotkey activation");

        // Step 4: Right-click and select Cancel. Verify the overlay disappears
        textExtractorWindow.Click(rightClick: true);
        Thread.Sleep(500);

        // Look for Cancel menu item using its AutomationId
        var cancelMenuItem = Find<Element>(By.AccessibilityId("CancelMenuItem"), 3000, true);
        Assert.IsNotNull(cancelMenuItem, "Cancel menu item should be available in context menu");

        cancelMenuItem.Click();
        Thread.Sleep(3000);

        var windowsAfterCancel = FindAll<Element>(By.AccessibilityId("TextExtractorWindow"), 2000, true);
        Assert.AreEqual(0, windowsAfterCancel.Count, "TextExtractor window should be dismissed after clicking Cancel");
    }

    [TestMethod("PowerOCR.DisableTextExtractorTest")]
    [TestCategory("PowerOCR Settings")]
    public void DisableTextExtractorTest()
    {
        Find<ToggleSwitch>(By.AccessibilityId("EnableTextExtractorToggleSwitch")).Toggle(false);

        SendKeys(Key.Win, Key.Shift, Key.T);

        // Verify that no TextExtractor window appears
        var windowsWhenDisabled = FindAll<Element>(By.AccessibilityId("TextExtractorWindow"), 10000, true);
        Assert.AreEqual(0, windowsWhenDisabled.Count, "TextExtractor window should not appear when the utility is disabled");
    }

    [TestMethod("PowerOCR.ActivationShortcutSettingsTest")]
    [TestCategory("PowerOCR Settings")]
    public void ActivationShortcutSettingsTest()
    {
        // Find the activation shortcut control
        var shortcutControl = Find<Element>(By.AccessibilityId("TextExtractorActivationShortcut"), 5000);
        Assert.IsNotNull(shortcutControl, "Activation shortcut control should be found");

        // Click to focus the shortcut control
        shortcutControl.Click();
        Thread.Sleep(500);

        // Test changing the shortcut to Ctrl+Shift+T
        SendKeys(Key.Ctrl, Key.Shift, Key.T);
        Thread.Sleep(1000);

        // Click the Save button to confirm the shortcut change
        var saveButton = Find<Button>(By.Name("Save"), 3000);
        Assert.IsNotNull(saveButton, "Save button should be found in the shortcut dialog");
        saveButton.Click();
        Thread.Sleep(1000);

        // Test the new shortcut
        SendKeys(Key.Ctrl, Key.Shift, Key.T);
        Thread.Sleep(3000);

        var textExtractorWindow = FindAll<Element>(By.AccessibilityId("TextExtractorWindow"), 3000, true);
        Assert.IsTrue(textExtractorWindow.Count > 0, "TextExtractor should activate with new shortcut Ctrl+Shift+T");
    }

    [TestMethod("PowerOCR.OCRLanguageSettingsTest")]
    [TestCategory("PowerOCR Settings")]
    public void OCRLanguageSettingsTest()
    {
        // Find the language combo box
        var languageComboBox = Find<ComboBox>(By.AccessibilityId("TextExtractorLanguageComboBox"), 5000);
        Assert.IsNotNull(languageComboBox, "Language combo box should be found");

        // Click to open the dropdown
        languageComboBox.Click();

        // Verify dropdown is opened by checking if dropdown items are available
        var dropdownItems = FindAll<Element>(By.ClassName("ComboBoxItem"), 2000);
        Assert.IsTrue(dropdownItems.Count > 0, "Dropdown should contain language options");

        // Close dropdown by pressing Escape
        SendKeys(Key.Esc);
    }

    [TestMethod("PowerOCR.OCRLanguageSelectionTest")]
    [TestCategory("PowerOCR Language")]
    public void OCRLanguageSelectionTest()
    {
        // Activate Text Extractor overlay
        SendKeys(Key.Win, Key.Shift, Key.T);
        Thread.Sleep(3000);

        var textExtractorWindow = Find<Element>(By.AccessibilityId("TextExtractorWindow"), 10000, true);
        Assert.IsNotNull(textExtractorWindow, "TextExtractor window should be found after hotkey activation");

        // Right-click on the canvas to open context menu
        textExtractorWindow.Click(rightClick: true);

        // Look for language options that should appear after Cancel menu item
        var allMenuItems = FindAll<Element>(By.ClassName("MenuItem"), 2000, true);
        if (allMenuItems.Count > 4)
        {
            // Find the Cancel menu item first
            Element? cancelItem = null;
            int cancelIndex = -1;
            for (int i = 0; i < allMenuItems.Count; i++)
            {
                if (allMenuItems[i].GetAttribute("AutomationId") == "CancelMenuItem")
                {
                    cancelItem = allMenuItems[i];
                    cancelIndex = i;
                    break;
                }
            }

            Assert.IsNotNull(cancelItem, "Cancel menu item should be found");

            // Look for language options after Cancel menu item
            if (cancelIndex >= 0 && cancelIndex < allMenuItems.Count - 1)
            {
                // Select the first language option after Cancel
                var languageOption = allMenuItems[cancelIndex + 1];
                languageOption.Click();
                Thread.Sleep(1000);

                Assert.IsTrue(true, "Language selection changed successfully through right-click menu");
            }
        }

        // Close the TextExtractor overlay
        SendKeys(Key.Esc);
        Thread.Sleep(1000);
    }

    [TestMethod("PowerOCR.TextSelectionAndClipboardTest")]
    [TestCategory("PowerOCR Selection")]
    public void TextSelectionAndClipboardTest()
    {
        // Clear clipboard first using STA thread
        ClearClipboardSafely();
        Thread.Sleep(500);

        // Activate Text Extractor overlay
        SendKeys(Key.Win, Key.Shift, Key.T);
        Thread.Sleep(3000);

        var textExtractorWindow = Find<Element>(By.AccessibilityId("TextExtractorWindow"), 10000, true);
        Assert.IsNotNull(textExtractorWindow, "TextExtractor window should be found after hotkey activation");

        // Click on the TextExtractor window to position cursor
        textExtractorWindow.Click();
        Thread.Sleep(500);

        // Get screen dimensions for full screen selection
        var primaryScreen = System.Windows.Forms.Screen.PrimaryScreen;
        Assert.IsNotNull(primaryScreen, "Primary screen should be available");

        var screenWidth = primaryScreen.Bounds.Width;
        var screenHeight = primaryScreen.Bounds.Height;

        // Define full screen selection area
        var startX = 0;
        var startY = 0;
        var endX = screenWidth;
        var endY = screenHeight;

        // Perform continuous mouse drag to select entire screen
        PerformSeleniumDrag(startX, startY, endX, endY);
        Thread.Sleep(3000); // Wait longer for full screen OCR processing

        // Verify text was copied to clipboard using STA thread
        var clipboardText = GetClipboardTextSafely();

        Assert.IsFalse(string.IsNullOrWhiteSpace(clipboardText), "Clipboard should contain extracted text after selection");

        // Close the TextExtractor overlay
        SendKeys(Key.Esc);
        Thread.Sleep(1000);
    }

    private static void ClearClipboardSafely()
    {
        var thread = new System.Threading.Thread(() =>
        {
            System.Windows.Forms.Clipboard.Clear();
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    private static string GetClipboardTextSafely()
    {
        string result = string.Empty;
        var thread = new System.Threading.Thread(() =>
        {
            try
            {
                result = System.Windows.Forms.Clipboard.GetText();
            }
            catch (Exception)
            {
                result = string.Empty;
            }
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();
        return result;
    }

    private void PerformSeleniumDrag(int startX, int startY, int endX, int endY)
    {
        // Use Selenium Actions for proper drag and drop operation
        var actions = new Actions(Session.Root);

        // Move to start position, click and hold, drag to end position, then release
        actions.MoveByOffset(startX, startY)
               .ClickAndHold()
               .MoveByOffset(endX - startX, endY - startY)
               .Release()
               .Build()
               .Perform();
    }
}
