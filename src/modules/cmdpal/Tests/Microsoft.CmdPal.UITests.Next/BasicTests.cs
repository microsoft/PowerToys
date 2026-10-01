// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UITests;

[TestClass]
public class BasicTests : CommandPaletteTestBase
{
    [TestMethod]
    [TestCategory("CmdPal")]
    public void BasicFileSearchTest()
    {
        SetSearchBox("files");

        var searchFileItem = CommandPaletteSession.Find<NavigationViewItem>("Search files");
        Assert.AreEqual("Search files", searchFileItem.Name);
        Step("Opening the Search files extension");
        DoubleClickResult(searchFileItem);

        SetFilesExtensionSearchBox("AppData");
        Assert.IsNotNull(CommandPaletteSession.Find<NavigationViewItem>("AppData"));
    }

    [TestMethod]
    [TestCategory("CmdPal")]
    public void BasicCalculatorTest()
    {
        SetSearchBox("calculator");

        var calculatorItem = CommandPaletteSession.Find<NavigationViewItem>("Calculator");
        Assert.AreEqual("Calculator", calculatorItem.Name);
        Step("Opening the Calculator extension");
        DoubleClickResult(calculatorItem);

        SetCalculatorExtensionSearchBox("1+2");
        Assert.IsNotNull(CommandPaletteSession.Find<NavigationViewItem>("3"));
    }

    [TestMethod]
    [TestCategory("CmdPal")]
    public void BasicTimeAndDateTest()
    {
        SetSearchBox("time and date");

        var timeAndDateItem = CommandPaletteSession.Find<NavigationViewItem>("Time and date");
        Assert.AreEqual("Time and date", timeAndDateItem.Name);
        Step("Opening the Time and date extension");
        DoubleClickResult(timeAndDateItem);

        SetTimeAndDateExtensionSearchBox("year");
        Assert.IsNotNull(CommandPaletteSession.Find<NavigationViewItem>(
            DateTime.Now.Year.ToString(CultureInfo.InvariantCulture)));
    }

    [TestMethod]
    [TestCategory("CmdPal")]
    public void BasicWindowsTerminalTest()
    {
        SetSearchBox("Windows Terminal");

        var terminalItem = CommandPaletteSession.Find<NavigationViewItem>("Open Windows Terminal profiles");
        Assert.AreEqual("Open Windows Terminal profiles", terminalItem.Name);
        Step("Verified the Windows Terminal profiles command");
    }

    [TestMethod]
    [TestCategory("CmdPal")]
    public void BasicWindowsSettingsTest()
    {
        SetSearchBox("Windows settings");

        var settingsItem = CommandPaletteSession.Find<NavigationViewItem>("Windows Settings");
        Assert.AreEqual("Windows Settings", settingsItem.Name);
        Step("Opening the Windows settings extension");
        DoubleClickResult(settingsItem);

        SetSearchBox("power");
        Assert.IsNotNull(CommandPaletteSession.Find<NavigationViewItem>("Power and sleep"));
    }

    [TestMethod]
    [TestCategory("CmdPal")]
    public void BasicRegistryTest()
    {
        SetSearchBox("Registry");

        var registryItem = CommandPaletteSession.Find<NavigationViewItem>("Registry Editor");
        Assert.AreEqual("Registry Editor", registryItem.Name);
        Step("Opening the Registry extension");
        DoubleClickResult(registryItem);
    }

    [TestMethod]
    [TestCategory("CmdPal")]
    public void BasicWindowsServicesTest()
    {
        SetSearchBox("Windows Services");

        var servicesItem = CommandPaletteSession.Find<NavigationViewItem>("Manage Windows services");
        Assert.AreEqual("Manage Windows services", servicesItem.Name);
        Step("Opening the Windows Services extension");
        DoubleClickResult(servicesItem);

        SetSearchBox("hyper-v");
        Assert.IsNotNull(CommandPaletteSession.Find<NavigationViewItem>("Hyper-V Heartbeat Service"));
    }

    [TestMethod]
    [TestCategory("CmdPal")]
    public void BasicWindowsSystemCommandsTest()
    {
        SetSearchBox("Windows System Commands");

        var systemCommandsItem = CommandPaletteSession.Find<NavigationViewItem>("Windows system commands");
        Assert.AreEqual("Windows system commands", systemCommandsItem.Name);
        Step("Opening the Windows System Commands extension");
        DoubleClickResult(systemCommandsItem);

        SetSearchBox("Sleep");
        Assert.IsNotNull(CommandPaletteSession.Find<NavigationViewItem>("Put computer to sleep"));
    }

    [TestMethod]
    [TestCategory("CmdPal")]
    public void DockSettingsAutoHideToggleTest()
    {
        OpenSettingsWindow();
        NavigateToDockSettings();

        var autoHideToggle = FindDockAutoHideToggle();
        var initialState = autoHideToggle.IsOn;
        try
        {
            if (autoHideToggle.IsOn == initialState)
            {
                autoHideToggle.Invoke();
            }

            Assert.IsTrue(autoHideToggle.WaitForProperty(
                "ToggleState",
                initialState ? "Off" : "On",
                timeoutMS: 5_000));

            CommandPaletteSession.Find<NavigationViewItem>("General").Click();
            NavigateToDockSettings();

            autoHideToggle = FindDockAutoHideToggle();
            Assert.AreEqual(!initialState, autoHideToggle.IsOn);
        }
        finally
        {
            try
            {
                autoHideToggle = FindDockAutoHideToggle();
                if (autoHideToggle.IsOn != initialState)
                {
                    autoHideToggle.Invoke();
                    Assert.IsTrue(
                        autoHideToggle.WaitForProperty(
                            "ToggleState",
                            initialState ? "On" : "Off",
                            timeoutMS: 5_000),
                        "Dock auto-hide setting did not return to its original state.");
                }
            }
            catch (Exception ex)
            {
                TestContext.WriteLine($"Could not restore Dock auto-hide setting: {ex.Message}");
            }
        }
    }
}
