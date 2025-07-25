// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Graphics;

namespace Microsoft.CmdPal.UITests;

public class WindowsSettingsTests : CommandPaletteTestBase
{
    public void EnterWindowsSettingsExtension()
    {
        SetSearchBox("Windows Settings");
        var searchFileItem = this.Find<NavigationViewItem>("Windows Settings");
        Assert.AreEqual(searchFileItem.Name, "Windows Settings");
        searchFileItem.DoubleClick();
    }

    public NavigationViewItem SearchWindowsSettingsByName(string name)
    {
        EnterWindowsSettingsExtension();
        SetWindowsSettingsExtensionSearchBox(name);
        var item = this.Find<NavigationViewItem>(name);
        Assert.IsNotNull(item, $"{name} setting not found.");
        return item;
    }

    [TestMethod]
    public void OpenDisplaySettingsTest()
    {
        const string settingName = "Display";
        var displaySettingItem = SearchWindowsSettingsByName(settingName);
        displaySettingItem.DoubleClick();

        var settings = FindWindowsSettingsWindow();
        Assert.IsNotNull(settings, "Display settings window not found.");
    }

    [TestMethod]
    public void OpenDisplaySettingsByPrimaryButtonTest()
    {
        const string settingName = "Display";
        var displaySettingItem = SearchWindowsSettingsByName(settingName);
        displaySettingItem.Click();

        var primaryButton = this.Find<Button>("Open Settings");
        Assert.IsNotNull(primaryButton, "Primary button not found.");
        primaryButton.Click();

        var settings = FindWindowsSettingsWindow();
        Assert.IsNotNull(settings, "Display settings window not found.");
    }

    [TestMethod]
    [STATestMethod]
    public void OpenDisplaySettingsSecondaryButtonTest()
    {
        const string settingName = "Display";
        var displaySettingItem = SearchWindowsSettingsByName(settingName);
        displaySettingItem.Click();

        var secondaryButton = this.Find<Button>("Copy command");
        Assert.IsNotNull(secondaryButton, "Secondary button not found.");
        secondaryButton.Click();

        var clipboardContent = System.Windows.Forms.Clipboard.GetText();
        Assert.IsTrue(clipboardContent.Contains("ms - settings:easeofaccess - display"), $"Clipboard content does not contain the expected command. clipboard: {clipboardContent}");
    }
}
