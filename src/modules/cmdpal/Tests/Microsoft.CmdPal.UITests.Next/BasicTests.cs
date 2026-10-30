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
        InvokeResultWithEnter(searchFileItem.Name);
        Assert.IsNotNull(CommandPaletteSession.Find<Button>("Back", timeoutMS: 45_000));

        SetFilesExtensionSearchBox("Downloads");
        Assert.IsNotNull(FindExactResult("Downloads", timeoutMS: 20_000));
    }

    [TestMethod]
    [TestCategory("CmdPal")]
    [DataRow("1+2", "3")]
    [DataRow("(12-3)*4", "36")]
    public void BasicCalculatorTest(string expression, string expectedResult)
    {
        var originalClipboard = ClipboardHelper.GetText();
        try
        {
            OpenExtension("calculator", "Calculator");
            SetCalculatorExtensionSearchBox(expression);
            ClipboardHelper.Clear();
            InvokeResultWithEnter(expectedResult);
            Assert.AreEqual(expectedResult, ClipboardHelper.WaitForText(string.Empty, timeoutMS: 10_000));
        }
        finally
        {
            ClipboardHelper.SetText(originalClipboard);
        }
    }

    [TestMethod]
    [TestCategory("CmdPal")]
    public void BasicTimeAndDateTest()
    {
        using var diagnostics = BeginNavigationDiagnostics();
        SetSearchBox("time and date");

        var timeAndDateItem = CommandPaletteSession.Find<NavigationViewItem>("Time and date");
        Assert.AreEqual("Time and date", timeAndDateItem.Name);
        InvokeResultWithEnter(timeAndDateItem.Name);
        Step("Waiting for the Time and date destination page's Back control");
        Assert.IsNotNull(CommandPaletteSession.Find<Button>("Back", timeoutMS: 45_000));

        SetTimeAndDateExtensionSearchBox("year");
        var culture = CultureInfo.CurrentCulture;
        var expectedYear = culture.Calendar.GetYear(DateTime.Now).ToString(culture);
        Step($"Waiting for the Time/Date result for year {expectedYear}");
        Assert.IsNotNull(CommandPaletteSession.Find<NavigationViewItem>(expectedYear));
    }

    [TestMethod]
    [TestCategory("CmdPal")]
    [DataRow("year")]
    [DataRow("week")]
    public void TimeAndDateCopiesCurrentResult(string query)
    {
        var originalClipboard = ClipboardHelper.GetText();
        try
        {
            OpenExtension("time and date", "Time and date");
            SetTimeAndDateExtensionSearchBox(query);
            var now = DateTime.Now;
            var culture = CultureInfo.CurrentCulture;
            var expected = query == "year"
                ? culture.Calendar.GetYear(now).ToString(culture)
                : (culture.DateTimeFormat.CalendarWeekRule == CalendarWeekRule.FirstFourDayWeek &&
                   culture.DateTimeFormat.FirstDayOfWeek == DayOfWeek.Monday
                    ? ISOWeek.GetWeekOfYear(now)
                    : culture.Calendar.GetWeekOfYear(
                        now,
                        culture.DateTimeFormat.CalendarWeekRule,
                        culture.DateTimeFormat.FirstDayOfWeek)).ToString(culture);
            ClipboardHelper.Clear();
            InvokeResultWithEnter(expected);
            Assert.AreEqual(expected, ClipboardHelper.WaitForText(string.Empty, timeoutMS: 10_000));
        }
        finally
        {
            ClipboardHelper.SetText(originalClipboard);
        }
    }

    [TestMethod]
    [TestCategory("CmdPal")]
    public void TimeAndDateNowMatchesLocalClock()
    {
        OpenExtension("time and date", "Time and date");
        SetTimeAndDateExtensionSearchBox("now");
        var culture = CultureInfo.CurrentCulture;
        TimeOnly observed = default;
        var found = CommandPaletteSession.WaitFor(
            () => CommandPaletteSession.FindAll<NavigationViewItem>(By.Name(":"), timeoutMS: 0)
                .Any(item => TimeOnly.TryParseExact(
                    item.Name,
                    [culture.DateTimeFormat.ShortTimePattern, culture.DateTimeFormat.LongTimePattern],
                    culture,
                    DateTimeStyles.None,
                    out observed)),
            timeoutMS: 15_000);
        Assert.IsTrue(found, "The 'now' query did not return a current local time.");
        var difference = Math.Abs(TimeOnly.FromDateTime(DateTime.Now).Ticks - observed.Ticks);
        var clockDifference = Math.Min(difference, TimeSpan.TicksPerDay - difference);
        Assert.IsTrue(
            clockDifference <= TimeSpan.FromSeconds(75).Ticks,
            "The 'now' result does not match the guest's local clock to minute precision.");
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
        using var diagnostics = BeginNavigationDiagnostics();
        SetSearchBox("Windows settings");

        var settingsItem = CommandPaletteSession.Find<NavigationViewItem>("Windows Settings");
        Assert.AreEqual("Windows Settings", settingsItem.Name);
        Step("Opening the Windows settings extension");
        InvokeResultWithEnter(settingsItem.Name);
        Step("Waiting for the Windows Settings destination page's Back control");
        Assert.IsNotNull(CommandPaletteSession.Find<Button>("Back", timeoutMS: 45_000));

        SetSearchBox("power");
        var powerSettings = CommandPaletteSession.Find<NavigationViewItem>("Power and sleep");
        Step("Opening the selected Windows Settings page");
        var windowsBefore = SnapshotWindowsSettings();
        try
        {
            InvokeResultWithEnter(powerSettings.Name);
            AssertWindowsSettingsOpened();
        }
        finally
        {
            CloseCreatedWindowsSettings(windowsBefore);
        }
    }

    [TestMethod]
    [TestCategory("CmdPal")]
    public void BasicRegistryTest()
    {
        OpenExtension("registry", "Browse the Windows registry");
        SetSearchBox(@"HKEY_CURRENT_USER\Software");
        SelectResult(@"HKEY_CURRENT_USER\Software");
        var originalClipboard = ClipboardHelper.GetText();
        try
        {
            ClipboardHelper.Clear();
            var copyKey = CommandPaletteSession.Find<Button>(By.AccessibilityId("SecondaryCommandButton"));
            Assert.AreEqual("Copy key name (path)", copyKey.Name);
            copyKey.Click();
            var copiedKey = ClipboardHelper.WaitForText(string.Empty, timeoutMS: 10_000);
            Assert.IsTrue(
                string.Equals(@"HKEY_CURRENT_USER\Software", copiedKey, StringComparison.OrdinalIgnoreCase),
                $"Expected the current-user Software key path, but the clipboard contained '{copiedKey}'.");
        }
        finally
        {
            ClipboardHelper.SetText(originalClipboard);
        }
    }

    [TestMethod]
    [TestCategory("CmdPal")]
    public void BasicWindowsServicesTest()
    {
        SetSearchBox("Windows Services");

        var servicesItem = CommandPaletteSession.Find<NavigationViewItem>("Manage Windows services");
        Assert.AreEqual("Manage Windows services", servicesItem.Name);
        Step("Opening the Windows Services extension");
        InvokeResultWithEnter(servicesItem.Name);
        Step("Waiting for the Windows Services destination page's Back control");
        Assert.IsNotNull(CommandPaletteSession.Find<Button>("Back", timeoutMS: 45_000));

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
        InvokeResultWithEnter(systemCommandsItem.Name);
        Step("Waiting for the Windows System Commands destination page's Back control");
        Assert.IsNotNull(CommandPaletteSession.Find<Button>("Back", timeoutMS: 45_000));

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
