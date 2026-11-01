// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OOBE.UITests;

[TestClass]
public sealed class OobeSectionTests : UITestBase
{
    private static Session? sharedWelcome;
    private Session welcome = null!;

    public OobeSectionTests()
        : base(PowerToysModule.PowerToysSettings, enableModules: [])
    {
    }

    protected override bool ReuseScopeAcrossTests => true;

    [TestInitialize]
    public void AttachOnboarding()
    {
        if (sharedWelcome is null ||
            !WindowsFinder.ListByApp(OobeTestHelper.SettingsProcess).Any(window => window.Hwnd == sharedWelcome.WindowHandle))
        {
            Step("Opening the class-shared OOBE window");
            sharedWelcome = OobeTestHelper.OpenWelcome(Session);
        }

        welcome = sharedWelcome;
    }

    [ClassCleanup(ClassCleanupBehavior.EndOfClass)]
    public static void ClearOnboardingSession()
    {
        sharedWelcome = null;
    }

    // Mirrors the module entries in Settings.UI\SettingsXAML\OobeWindow.xaml.
    [TestMethod]
    [TestCategory("OOBE")]
    [DataRow("Advanced Paste")]
    [DataRow("Always On Top")]
    [DataRow("Awake")]
    [DataRow("Color Picker")]
    [DataRow("Command Palette")]
    [DataRow("Command Not Found")]
    [DataRow("Crop And Lock")]
    [DataRow("Environment Variables")]
    [DataRow("FancyZones")]
    [DataRow("File Locksmith")]
    [DataRow("File Explorer Add-ons")]
    [DataRow("Grab And Move")]
    [DataRow("Hosts File Editor")]
    [DataRow("Image Resizer")]
    [DataRow("Keyboard Manager")]
    [DataRow("Light Switch")]
    [DataRow("Mouse utilities")]
    [DataRow("Mouse Without Borders")]
    [DataRow("New+")]
    [DataRow("Peek")]
    [DataRow("Power Display")]
    [DataRow("PowerRename")]
    [DataRow("PowerToys Run")]
    [DataRow("Quick Accent")]
    [DataRow("Registry Preview")]
    [DataRow("Screen Ruler")]
    [DataRow("Shortcut Guide")]
    [DataRow("Text Extractor")]
    [DataRow("Window Hopper")]
    [DataRow("Workspaces")]
    [DataRow("ZoomIt")]
    public void ModulePageRendersInsideOobe(string section)
    {
        Step($"Rendering '{section}' inside OOBE");
        var heading = OobeTestHelper.SelectSection(welcome, section);
        Assert.IsTrue(heading.WaitForProperty("IsOffscreen", "False", 5_000), $"'{section}' has no visible OOBE heading.");
        var description = welcome.Find<TextBlock>(By.AccessibilityId("DescriptionTxt"));
        Assert.IsFalse(string.IsNullOrWhiteSpace(description.GetValue()), $"'{section}' has no OOBE description.");
        Assert.IsTrue(welcome.Has<Button>(By.Name("Open Settings"), 5_000), $"'{section}' did not render its OOBE page content.");
    }

    [TestMethod]
    [TestCategory("OOBE")]
    public void GettingStartedPageRenders()
    {
        Step("Rendering Getting started");
        welcome.Find<NavigationViewItem>(By.Name("Getting started")).Invoke(msPostAction: 0);
        var heading = welcome.Find<TextBlock>(By.AccessibilityId("WelcomeTitle"));
        Assert.IsTrue(heading.WaitForProperty("IsOffscreen", "False", 5_000), "The Getting started heading was not visible.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(heading.GetValue()), "The Getting started heading was empty.");
        Assert.IsTrue(welcome.Has<Button>(By.Name("Configure PowerToys"), 5_000), "Getting started did not render its actions.");
    }

    [TestMethod]
    [TestCategory("OOBE")]
    public void WelcomeReopensFromSettings()
    {
        Step("Closing and reopening Welcome using the Settings entry point");
        OobeTestHelper.CloseWindow(welcome);
        sharedWelcome = OobeTestHelper.OpenWelcome(Session);
        welcome = sharedWelcome;
    }

    [TestMethod]
    [TestCategory("OOBE")]
    public void WhatsNewFooterOpensReleaseNotesWindow()
    {
        Step("Ensuring the What's New window is not already open");
        Assert.IsTrue(
            WindowControl.TryCloseByApp(
                OobeTestHelper.SettingsProcess,
                window => string.Equals(window.Title, OobeTestHelper.WhatsNewTitle, StringComparison.OrdinalIgnoreCase),
                timeoutMS: 15_000),
            "Could not close a stale What's New window before testing the OOBE footer.");
        Assert.IsFalse(
            WindowsFinder.ListByApp(OobeTestHelper.SettingsProcess)
                .Any(window => string.Equals(window.Title, OobeTestHelper.WhatsNewTitle, StringComparison.OrdinalIgnoreCase)),
            "The What's New footer test requires no preexisting release-notes window.");
        Step("Opening What's new from OOBE");
        var link = welcome.Find<NavigationViewItem>(By.AccessibilityId("WhatIsNewNavItem"));
        link.ScrollIntoView();
        OobeTestHelper.RequireForeground(welcome);
        link.MouseClick(msPostAction: 0);
        var whatsNew = OobeTestHelper.WaitForSettingsWindow(OobeTestHelper.WhatsNewTitle);
        OobeTestHelper.CloseWindow(whatsNew);
    }

    private void Step(string message) => TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}");
}
