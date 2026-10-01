// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UITests;

[TestClass]
public class BasicTests : CommandPaletteTestBase
{
    public BasicTests()
    {
    }

    [TestMethod]
    [TestCategory("SamplePagesExtension")]
    [DataRow("closed", true)]
    [DataRow("filter", false)]
    [DataRow("list", false)]
    public void ContextShortcut_NavigatesOnce(string menuFocus, bool useEscape)
    {
        SetSearchBox("Sample Pages");
        if (!this.HasOne<NavigationViewItem>("Sample Pages"))
        {
            Assert.Inconclusive("Register and enable SamplePagesExtension to run this test.");
        }

        if (menuFocus != "closed")
        {
            SendKeys(Key.Ctrl, Key.K);
            Assert.IsNotNull(this.Find<NavigationViewItem>("Open list sample with shortcut"));

            if (menuFocus == "list")
            {
                SendKeys(Key.Shift, Key.Tab);
                Assert.IsFalse(bool.Parse(this.Find<TextBox>(By.AccessibilityId("ContextFilterBox")).GetAttribute("HasKeyboardFocus")));
            }
        }

        SendKeys(Key.Ctrl, Key.Num1);
        Assert.IsNotNull(this.Find<NavigationViewItem>("This is a basic item in the list"));

        if (useEscape)
        {
            SendKeys(Key.Esc);
        }
        else
        {
            this.Find<Button>("Back").Click();
        }

        Assert.IsNotNull(this.Find<NavigationViewItem>("Sample Pages"), "One Back or Escape must return to the root page.");
    }

    [TestMethod]
    [TestCategory("SamplePagesExtension")]
    [DataRow(false)]
    [DataRow(true)]
    public void ContextShortcut_OpensSubmenuBeforeInvokingChild(bool openContextMenu)
    {
        SetSearchBox("Sample Pages");
        if (!this.HasOne<NavigationViewItem>("Sample Pages"))
        {
            Assert.Inconclusive("Register and enable SamplePagesExtension to run this test.");
        }

        if (openContextMenu)
        {
            SendKeys(Key.Ctrl, Key.K);
            Assert.IsNotNull(this.Find<NavigationViewItem>("Shortcut submenu"));
        }

        SendKeys(Key.Ctrl, Key.Num2);
        Assert.IsNotNull(this.Find<NavigationViewItem>("Open nested list sample"));
        SendKeys(Key.Ctrl, Key.Num1);
        Assert.IsNotNull(this.Find<NavigationViewItem>("Details with rich content (Small)"));
        this.Find<Button>("Back").Click();
        Assert.IsNotNull(this.Find<NavigationViewItem>("Sample Pages"));
    }

    [TestMethod]
    [TestCategory("SamplePagesExtension")]
    public void ContextSubmenuEscape_ClosesFlyoutAndRestoresFocus()
    {
        SetSearchBox("Sample Pages");
        if (!this.HasOne<NavigationViewItem>("Sample Pages"))
        {
            Assert.Inconclusive("Register and enable SamplePagesExtension to run this test.");
        }

        SendKeys(Key.Ctrl, Key.Num2);
        Assert.IsNotNull(this.Find<NavigationViewItem>("Open nested list sample"));
        SendKeys(Key.Esc);
        Assert.IsFalse(this.HasOne<NavigationViewItem>("Open nested list sample"));
        Assert.IsTrue(bool.Parse(this.Find<TextBox>(By.AccessibilityId("MainSearchBox")).GetAttribute("HasKeyboardFocus")));
    }

    [TestMethod]
    [TestCategory("SamplePagesExtension")]
    [DataRow(false)]
    [DataRow(true)]
    public void ContextSecondaryShortcut_InvokesSecondaryInsteadOfHighlightedPrimary(bool focusList)
    {
        SetSearchBox("Sample Pages");
        if (!this.HasOne<NavigationViewItem>("Sample Pages"))
        {
            Assert.Inconclusive("Register and enable SamplePagesExtension to run this test.");
        }

        SendKeys(Key.Ctrl, Key.K);
        Assert.IsNotNull(this.Find<NavigationViewItem>("Open list sample with shortcut"));
        if (focusList)
        {
            SendKeys(Key.Shift, Key.Tab);
            Assert.IsFalse(bool.Parse(this.Find<TextBox>(By.AccessibilityId("ContextFilterBox")).GetAttribute("HasKeyboardFocus")));
        }

        SendKeys(Key.Ctrl, Key.Enter);
        Assert.IsNotNull(this.Find<NavigationViewItem>("This is a basic item in the list"));
        this.Find<Button>("Back").Click();
        Assert.IsNotNull(this.Find<NavigationViewItem>("Sample Pages"));
    }

    [TestMethod]
    public void BasicFileSearchTest()
    {
        SetSearchBox("files");

        var searchFileItem = this.Find<NavigationViewItem>("Search files");
        Assert.AreEqual("Search files", searchFileItem.Name);
        searchFileItem.DoubleClick();

        SetFilesExtensionSearchBox("AppData");

        Assert.IsNotNull(this.Find<NavigationViewItem>("AppData"));
    }

    [TestMethod]
    public void BasicCalculatorTest()
    {
        SetSearchBox("calculator");

        var searchFileItem = this.Find<NavigationViewItem>("Calculator");
        Assert.AreEqual("Calculator", searchFileItem.Name);
        searchFileItem.DoubleClick();

        SetCalculatorExtensionSearchBox("1+2");

        Assert.IsNotNull(this.Find<NavigationViewItem>("3"));
    }

    [TestMethod]
    public void BasicTimeAndDateTest()
    {
        SetSearchBox("time and date");

        var searchFileItem = this.Find<NavigationViewItem>("Time and date");
        Assert.AreEqual("Time and date", searchFileItem.Name);
        searchFileItem.DoubleClick();

        SetTimeAndDaterExtensionSearchBox("year");

        Assert.IsNotNull(this.Find<NavigationViewItem>("2026"));
    }

    [TestMethod]
    public void BasicWindowsTerminalTest()
    {
        SetSearchBox("Windows Terminal");

        var searchFileItem = this.Find<NavigationViewItem>("Open Windows Terminal profiles");
        Assert.AreEqual("Open Windows Terminal profiles", searchFileItem.Name);
        searchFileItem.DoubleClick();

        // SetSearchBox("PowerShell");
        // Assert.IsNotNull(this.Find<NavigationViewItem>("PowerShell"));
    }

    [TestMethod]
    public void BasicWindowsSettingsTest()
    {
        SetSearchBox("Windows settings");

        var searchFileItem = this.Find<NavigationViewItem>("Windows settings");
        Assert.AreEqual("Windows settings", searchFileItem.Name);
        searchFileItem.DoubleClick();

        SetSearchBox("power");

        Assert.IsNotNull(this.Find<NavigationViewItem>("Power and sleep"));
    }

    [TestMethod]
    public void BasicRegistryTest()
    {
        SetSearchBox("Registry");

        var searchFileItem = this.Find<NavigationViewItem>("Registry");
        Assert.AreEqual("Registry", searchFileItem.Name);
        searchFileItem.DoubleClick();

        // Type the string will cause strange behavior.so comment it out for now.
        // SetSearchBox(@"HKEY_LOCAL_MACHINE");
        // Assert.IsNotNull(this.Find<NavigationViewItem>(@"HKEY_LOCAL_MACHINE\SECURITY"));
    }

    [TestMethod]
    public void BasicWindowsServicesTest()
    {
        SetSearchBox("Windows Services");

        var searchFileItem = this.Find<NavigationViewItem>("Windows Services");
        Assert.AreEqual("Windows Services", searchFileItem.Name);
        searchFileItem.DoubleClick();

        SetSearchBox("hyper-v");

        Assert.IsNotNull(this.Find<NavigationViewItem>("Hyper-V Heartbeat Service"));
    }

    [TestMethod]
    public void BasicWindowsSystemCommandsTest()
    {
        SetSearchBox("Windows System Commands");

        var searchFileItem = this.Find<NavigationViewItem>("Windows System Commands");
        Assert.AreEqual("Windows System Commands", searchFileItem.Name);
        searchFileItem.DoubleClick();

        SetSearchBox("Sleep");

        Assert.IsNotNull(this.Find<NavigationViewItem>("Put computer to sleep"));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SettingsSearch_SubmissionOpensResults(bool showAllResults)
    {
        OpenSettingsWindow();
        this.Find<NavigationViewItem>("General", global: true).Click();
        this.Find<TextBox>("Search settings, commands, and extensions", global: true).SetText("preview");
        var showAll = this.Find(By.AccessibilityId("SettingsSearchShowAllResults"), global: true);
        StringAssert.StartsWith(showAll.Name, "Show ");

        if (showAllResults)
        {
            showAll.Click();
        }
        else
        {
            SendKeys(Key.Enter);
        }

        Assert.IsNotNull(this.Find("Results for 'preview'", global: true));
        Assert.IsNotNull(this.Find("Personalization › Interaction", global: true));
        Assert.IsFalse(bool.Parse(this.Find<TextBox>("Search settings, commands, and extensions", global: true).GetAttribute("HasKeyboardFocus")));

        this.Find<TextBox>("Search settings, commands, and extensions", global: true).SetText("preview");
        if (showAllResults)
        {
            this.Find(By.AccessibilityId("SettingsSearchShowAllResults"), global: true).Click();
        }
        else
        {
            SendKeys(Key.Enter);
        }

        Assert.IsNotNull(this.Find("Results for 'preview'", global: true));
        this.Find<Button>("Back", global: true).Click();
        Assert.IsNotNull(this.Find(By.AccessibilityId("CmdPal_GeneralPage_ActivationKey"), global: true));
        this.Find<TextBox>("Search settings, commands, and extensions", global: true).SetText("preview");
        SendKeys(Key.Enter);
        Assert.IsNotNull(this.Find("Results for 'preview'", global: true));

        for (var visit = 0; visit < 2; visit++)
        {
            this.Find("Automatically expand app details", global: true).Click();
            Assert.IsNotNull(this.Find<ToggleSwitch>(By.AccessibilityId("CmdPal_AppearancePage_ShowAppDetails"), global: true));

            // Reinvoking the selected item makes WinUI invoke it again when search clears selection.
            this.Find<NavigationViewItem>("Personalization", global: true).Click();
            this.Find<Button>("Back", global: true).Click();
            Assert.IsNotNull(this.Find("Results for 'preview'", global: true));
            Assert.IsNotNull(this.Find("Personalization › Interaction", global: true));
        }

        this.Find<TextBox>("Search settings, commands, and extensions", global: true).SetText("zqxzqxzqx");
        SendKeys(Key.Enter);
        Assert.IsNotNull(this.Find("Results for 'zqxzqxzqx'", global: true));
        Assert.IsNotNull(this.Find("No settings, commands, or extensions found. Try a different search term.", global: true));
    }

    [TestMethod]
    [DataRow("General", "CmdPal_GeneralPage_ActivationKey")]
    [DataRow("Calculator", "CmdPal_ExtensionPage_Enable")]
    public void SettingsSearch_PageAndProviderSuggestionsMoveFocus(string query, string destinationId)
    {
        OpenSettingsWindow();
        this.Find<NavigationViewItem>("Personalization", global: true).Click();
        this.Find<TextBox>("Search settings, commands, and extensions", global: true).SetText(query);
        Assert.IsNotNull(this.Find(By.AccessibilityId("SettingsSearchShowAllResults"), global: true));
        SendKeys(Key.Down);
        SendKeys(Key.Enter);

        Assert.IsNotNull(this.Find(By.AccessibilityId(destinationId), global: true));
        Assert.IsFalse(bool.Parse(this.Find<TextBox>("Search settings, commands, and extensions", global: true).GetAttribute("HasKeyboardFocus")));
    }

    [TestMethod]
    public void SettingsSearch_CommandNameAndChangedAliasOpenCommandSettings()
    {
        OpenSettingsWindow();
        this.Find<TextBox>("Search settings, commands, and extensions", global: true).SetText("calculator commands");
        Assert.IsNotNull(this.Find(By.AccessibilityId("SettingsSearchShowAllResults"), global: true));
        SendKeys(Key.Down);
        SendKeys(Key.Enter);

        var aliasBox = this.Find<TextBox>(By.AccessibilityId("CmdPal_ExtensionPage_AliasText"), global: true);
        Assert.IsFalse(bool.Parse(this.Find<TextBox>("Search settings, commands, and extensions", global: true).GetAttribute("HasKeyboardFocus")));
        var originalAlias = aliasBox.Text;
        try
        {
            aliasBox.SetText("cmdpal-settings-search-test");
            this.Find<TextBox>("Search settings, commands, and extensions", global: true).SetText("cmdpal-settings-search-test");
            Assert.IsNotNull(this.Find(By.AccessibilityId("SettingsSearchShowAllResults"), global: true));
            SendKeys(Key.Down);
            SendKeys(Key.Enter);

            Assert.AreEqual("cmdpal-settings-search-test", this.Find<TextBox>(By.AccessibilityId("CmdPal_ExtensionPage_AliasText"), global: true).Text);
        }
        finally
        {
            aliasBox.SetText(originalAlias);
        }
    }

    [TestMethod]
    public void DockSettingsAutoHideToggleTest()
    {
        OpenSettingsWindow();
        NavigateToDockSettings();

        var autoHideToggle = FindDockAutoHideToggle();
        Assert.IsNotNull(autoHideToggle);

        var initialState = autoHideToggle.IsOn;
        autoHideToggle.Toggle(!initialState);
        Assert.AreEqual(!initialState, autoHideToggle.IsOn);

        this.Find<NavigationViewItem>("General").Click();
        NavigateToDockSettings();

        autoHideToggle = FindDockAutoHideToggle();
        Assert.IsNotNull(autoHideToggle);
        Assert.AreEqual(!initialState, autoHideToggle.IsOn);

        autoHideToggle.Toggle(initialState);
        Assert.AreEqual(initialState, autoHideToggle.IsOn);
    }
}
