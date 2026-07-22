// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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

        try
        {
            // Read the current language from the overlay toolbar ComboBox.
            var languageComboBox = Find<ComboBox>(
                By.AccessibilityId("OCROverlayLanguagesComboBox"),
                5000,
                true);
            languageComboBox.Click();

            var comboBoxItems = FindAll<Element>(By.ClassName("ComboBoxItem"), 3000, true)
                .Where(item => item.Displayed)
                .ToList();
            Assert.IsTrue(comboBoxItems.Count > 0, "The overlay language ComboBox should contain OCR languages.");

            var selectedItems = comboBoxItems.Where(item => item.Selected).ToList();
            Assert.AreEqual(1, selectedItems.Count, "The overlay language ComboBox should have one selected language.");
            if (comboBoxItems.Count < 2)
            {
                Assert.Inconclusive("At least two installed OCR languages are required to verify a language change.");
            }

            int selectedLanguageIndex = comboBoxItems.FindIndex(item => item.Selected);
            int targetLanguageIndex = selectedLanguageIndex == 0 ? 1 : 0;
            var selectedItem = comboBoxItems[selectedLanguageIndex];
            var targetItem = comboBoxItems[targetLanguageIndex];
            var selectedLanguageName = selectedItem.Name;
            var targetLanguageName = targetItem.Name;
            SendKeys(Key.Esc);

            // Open the context menu from the focused ComboBox using only the keyboard.
            // This exercises the routed Shift+F10 path rather than the pointer path.
            SendKeys(Key.Shift, Key.F10);

            var selectedMenuItem = FindAll<Element>(By.Name(selectedLanguageName), 3000, true)
                .FirstOrDefault(item =>
                    item.Displayed
                    && item.AutomationId.StartsWith("OCRLanguageMenuItem_", StringComparison.Ordinal));
            Assert.IsNotNull(selectedMenuItem, $"The context menu should contain the selected '{selectedLanguageName}' OCR language.");
            Assert.AreEqual(
                "1",
                selectedMenuItem.GetAttribute("Toggle.ToggleState"),
                "The current OCR language should be marked as selected in the context menu.");

            var targetMenuItem = FindAll<Element>(By.Name(targetLanguageName), 3000, true)
                .FirstOrDefault(item =>
                    item.Displayed
                    && item.AutomationId.StartsWith("OCRLanguageMenuItem_", StringComparison.Ordinal));
            Assert.IsNotNull(targetMenuItem, $"The context menu should contain the '{targetLanguageName}' OCR language.");
            Assert.AreEqual(
                "0",
                targetMenuItem.GetAttribute("Toggle.ToggleState"),
                "The target OCR language should not be selected before activation.");

            // Navigate from the first language item and activate the target without a pointer.
            SendKeys(Key.Home);
            for (int index = 0; index < targetLanguageIndex; index++)
            {
                SendKeys(Key.Down);
            }

            SendKeys(Key.Enter);

            // Re-open the toolbar ComboBox and verify its selected item changed.
            languageComboBox = Find<ComboBox>(
                By.AccessibilityId("OCROverlayLanguagesComboBox"),
                5000,
                true);
            languageComboBox.Click();

            var updatedSelectedItems = FindAll<Element>(By.ClassName("ComboBoxItem"), 3000, true)
                .Where(item => item.Displayed)
                .Where(item => item.Selected)
                .ToList();
            Assert.AreEqual(1, updatedSelectedItems.Count, "The overlay language ComboBox should keep one selected language.");
            Assert.AreEqual(
                targetLanguageName,
                updatedSelectedItems[0].Name,
                "Selecting a context-menu language should update the toolbar ComboBox selection.");
        }
        finally
        {
            // Escape dismisses an open flyout first. Only send it again when the overlay
            // is still present so the underlying Settings window never receives the key.
            SendKeys(Key.Esc);
            Thread.Sleep(250);
            if (FindAll<Element>(By.AccessibilityId("TextExtractorWindow"), 1000, true).Count > 0)
            {
                SendKeys(Key.Esc);
                Thread.Sleep(1000);
            }
        }
    }

    [TestMethod("PowerOCR.ToolbarModes")]
    [TestCategory("PowerOCR Toolbar")]
    public void ToolbarModesTest()
    {
        // Activate Text Extractor overlay
        SendKeys(Key.Win, Key.Shift, Key.T);
        Thread.Sleep(3000);

        var textExtractorWindow = Find<Element>(By.AccessibilityId("TextExtractorWindow"), 10000, true);
        Assert.IsNotNull(textExtractorWindow, "TextExtractor window should appear after hotkey activation");

        // Verify SingleLine mode is independent of its initial state
        var singleLineButton = Find<Element>(By.AccessibilityId("SingleLineToggleButton"), 5000, true);
        Assert.IsNotNull(singleLineButton, "SingleLine toggle button should be found on the toolbar");
        if (singleLineButton.Selected)
        {
            singleLineButton.Click();
            Thread.Sleep(500);

            singleLineButton = Find<Element>(By.AccessibilityId("SingleLineToggleButton"), 5000, true);
            Assert.IsFalse(singleLineButton.Selected, "SingleLine toggle button should be deselected after the reset click");
        }

        singleLineButton.Click();
        Thread.Sleep(500);

        singleLineButton = Find<Element>(By.AccessibilityId("SingleLineToggleButton"), 5000, true);
        Assert.IsTrue(singleLineButton.Selected, "SingleLine toggle button should be selected after click");

        // Verify Table mode is independent of its initial state
        var tableButton = Find<Element>(By.AccessibilityId("TableToggleButton"), 5000, true);
        Assert.IsNotNull(tableButton, "Table toggle button should be found on the toolbar");
        if (tableButton.Selected)
        {
            tableButton.Click();
            Thread.Sleep(500);

            tableButton = Find<Element>(By.AccessibilityId("TableToggleButton"), 5000, true);
            Assert.IsFalse(tableButton.Selected, "Table toggle button should be deselected after the reset click");
        }

        tableButton.Click();
        Thread.Sleep(500);

        tableButton = Find<Element>(By.AccessibilityId("TableToggleButton"), 5000, true);
        Assert.IsTrue(tableButton.Selected, "Table toggle button should be selected after click");

        // Dismiss the overlay and verify it disappears
        SendKeys(Key.Esc);
        Thread.Sleep(3000);

        var windowsAfterEscape = FindAll<Element>(By.AccessibilityId("TextExtractorWindow"), 2000, true);
        Assert.AreEqual(0, windowsAfterEscape.Count, "TextExtractor window should be dismissed after pressing Escape");
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

        // Use the selection canvas for a stable, bounds-aware drag
        var selectionCanvas = Find<Pane>(
            By.AccessibilityId("RegionClickCanvas"),
            5000,
            true);
        Assert.IsNotNull(selectionCanvas.Rect, "Selection canvas bounds should be available.");
        var bounds = selectionCanvas.Rect.Value;
        selectionCanvas.Drag(
            Math.Min(300, Math.Max(50, bounds.Width / 4)),
            Math.Min(200, Math.Max(50, bounds.Height / 4)));
        Thread.Sleep(3000); // Wait for OCR processing to complete

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
}
