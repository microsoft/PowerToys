// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using Microsoft.CmdPal.Ext.TimeDate;
using Microsoft.CmdPal.Ext.TimeDate.Helpers;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.TimeDate.UnitTests;

[TestClass]
[System.Diagnostics.CodeAnalysis.SuppressMessage("IDisposable", "CA1001:Types that own disposable fields should be disposable", Justification = "Disposed in TestCleanup")]
public class NowDockBandTests
{
    private static readonly DateTime FixedTime = new DateTime(2025, 7, 1, 14, 5, 32);

    private CultureInfo _originalCulture = null!;
    private CultureInfo _originalUiCulture = null!;
    private NowDockBand? _band;

    [TestInitialize]
    public void Setup()
    {
        _originalCulture = CultureInfo.CurrentCulture;
        _originalUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = new CultureInfo("en-US", false);
        CultureInfo.CurrentUICulture = new CultureInfo("en-US", false);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _band?.Dispose();
        _band = null;
        CultureInfo.CurrentCulture = _originalCulture;
        CultureInfo.CurrentUICulture = _originalUiCulture;
    }

    // Default numbering system is ISO 8601.
    private static string FixedTimeWeek() => ISOWeek.GetWeekOfYear(FixedTime).ToString(CultureInfo.CurrentCulture);

    [TestMethod]
    public void Constructor_TitleIsSetImmediately()
    {
        _band = new NowDockBand(new Settings(), clock: () => FixedTime);

        Assert.AreEqual("2:05 PM", _band.Title);
        Assert.IsFalse(string.IsNullOrEmpty(_band.Subtitle));
    }

    [TestMethod]
    public void UpdateText_LongTimeFormat_TitleContainsSeconds()
    {
        _band = new NowDockBand(new Settings(dockClockWithSecond: true), clock: () => FixedTime);

        _band.UpdateText();

        Assert.AreEqual("2:05:32 PM", _band.Title);
    }

    [TestMethod]
    public void UpdateText_ShortDateFormat_SubtitleIsShortDate()
    {
        _band = new NowDockBand(new Settings(), clock: () => FixedTime);

        _band.UpdateText();

        Assert.AreEqual("7/1/2025", _band.Subtitle);
    }

    [TestMethod]
    public void UpdateText_FiresOnUpdatedCallback()
    {
        var callbackFired = false;
        _band = new NowDockBand(new Settings(), onUpdated: () => callbackFired = true, clock: () => FixedTime);

        callbackFired = false; // reset — constructor already fired it once during synchronous UpdateText()

        _band.UpdateText();

        Assert.IsTrue(callbackFired);
    }

    [TestMethod]
    public void UpdateText_CallbackFiredAfterAssignments()
    {
        var titleAtCallback = string.Empty;
        _band = new NowDockBand(
            new Settings(),
            onUpdated: () => titleAtCallback = _band?.Title ?? string.Empty,
            clock: () => FixedTime);

        titleAtCallback = string.Empty; // reset after construction callback

        _band.UpdateText();

        Assert.IsFalse(string.IsNullOrEmpty(titleAtCallback), "Title should be assigned before callback fires");
    }

    [TestMethod]
    public void UpdateText_CopyCommandsUpdated()
    {
        _band = new NowDockBand(new Settings(), clock: () => FixedTime);

        _band.UpdateText();

        Assert.AreEqual(_band.Title, _band.CopyTimeCommand.Text);
        Assert.AreEqual(_band.Subtitle, _band.CopyDateCommand.Text);
    }

    [DataTestMethod]
    [DataRow("de-DE")]
    [DataRow("fr-FR")]
    [DataRow("ar-SA")]
    public void UpdateText_CultureSmoke_TitleNonEmpty(string cultureName)
    {
        // Culture MUST be set before construction — constructor calls UpdateText() synchronously
        CultureInfo.CurrentCulture = new CultureInfo(cultureName, false);
        CultureInfo.CurrentUICulture = new CultureInfo(cultureName, false);

        _band = new NowDockBand(new Settings(), clock: () => FixedTime);

        Assert.IsFalse(string.IsNullOrEmpty(_band.Title), $"Title should be non-empty for culture '{cultureName}'");
        Assert.IsFalse(string.IsNullOrEmpty(_band.Subtitle), $"Subtitle should be non-empty for culture '{cultureName}'");
    }

    [TestMethod]
    public void UpdateSettings_EnablingSeconds_TitleIncludesSeconds()
    {
        var settings = new Settings(dockClockWithSecond: false);
        _band = new NowDockBand(settings, clock: () => FixedTime);

        Assert.AreEqual("2:05 PM", _band.Title, "Precondition: seconds hidden by default");

        settings.DockClockWithSecond = true;
        _band.UpdateSettings();

        Assert.AreEqual("2:05:32 PM", _band.Title, "Title should update live to include seconds");
    }

    [TestMethod]
    public void UpdateSettings_DisablingSeconds_TitleDropsSeconds()
    {
        var settings = new Settings(dockClockWithSecond: true);
        _band = new NowDockBand(settings, clock: () => FixedTime);

        Assert.AreEqual("2:05:32 PM", _band.Title, "Precondition: seconds shown");

        settings.DockClockWithSecond = false;
        _band.UpdateSettings();

        Assert.AreEqual("2:05 PM", _band.Title, "Title should update live to drop seconds");
    }

    [TestMethod]
    public void UpdateSettings_NoChange_FiresNoCallback()
    {
        var callbackCount = 0;
        _band = new NowDockBand(new Settings(dockClockWithSecond: false), onUpdated: () => callbackCount++, clock: () => FixedTime);

        callbackCount = 0; // reset after construction callback

        _band.UpdateSettings();

        Assert.AreEqual(0, callbackCount, "A no-op settings change should not refresh the band");
    }

    [TestMethod]
    public void UpdateSettings_ChangedDateMode_RefreshesTheSubtitle()
    {
        var settings = new Settings(clockBandDateMode: 0);
        _band = new NowDockBand(settings, clock: () => FixedTime);

        Assert.AreEqual("7/1/2025", _band.Subtitle, "Precondition: system date only");

        settings.ClockBandDateMode = 1;
        _band.UpdateSettings();

        StringAssert.Contains(_band.Subtitle, FixedTimeWeek());
    }

    [TestMethod]
    public void SystemDateModeShowsOnlyTheDate()
    {
        var settings = new Settings(clockBandDateMode: 0);

        _band = new NowDockBand(settings, clock: () => FixedTime);

        Assert.AreEqual("7/1/2025", _band.Subtitle);
        Assert.AreEqual(2, _band.MoreCommands.Length);
    }

    [TestMethod]
    public void WeekNumberModeAppendsTheWeekNumber()
    {
        var settings = new Settings(clockBandDateMode: 1);

        _band = new NowDockBand(settings, clock: () => FixedTime);

        StringAssert.Contains(_band.Subtitle, FixedTimeWeek());
        Assert.AreEqual(3, _band.MoreCommands.Length);
    }

    [TestMethod]
    public void WeekNumberModeRespectsCustomFirstWeekAndFirstDaySettings()
    {
        // Custom week mode with first day rule and Monday
        var settings = new Settings(firstWeekOfYear: 0, firstDayOfWeek: 1, clockBandDateMode: 3);

        _band = new NowDockBand(settings, clock: () => FixedTime);

        var expectedWeek = CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(
            FixedTime,
            CalendarWeekRule.FirstDay,
            DayOfWeek.Monday);
        StringAssert.Contains(_band.Subtitle, expectedWeek.ToString(CultureInfo.CurrentCulture));
    }

    [TestMethod]
    public void IsoWeekDateModeShowsTheIsoWeekDate()
    {
        var settings = new Settings(clockBandDateMode: 4);

        _band = new NowDockBand(settings, clock: () => FixedTime);

        Assert.AreEqual(TimeAndDateHelper.GetIsoWeekDateString(FixedTime), _band.Subtitle);
        Assert.AreEqual(3, _band.MoreCommands.Length);
    }

    [TestMethod]
    public void UsWeekModeAppendsTheUsWeekNumber()
    {
        var settings = new Settings(clockBandDateMode: 2);

        _band = new NowDockBand(settings, clock: () => FixedTime);

        var expectedWeek = CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
            FixedTime,
            CalendarWeekRule.FirstDay,
            DayOfWeek.Sunday);
        StringAssert.Contains(_band.Subtitle, expectedWeek.ToString(CultureInfo.CurrentCulture));
        Assert.AreEqual(3, _band.MoreCommands.Length);
    }

    [TestMethod]
    public void CustomFormatModeOverridesTheDateLine()
    {
        var settings = new Settings(clockBandDateMode: 5, customDateFormatInClockBand: "yyyy-MM-dd");

        _band = new NowDockBand(settings, clock: () => FixedTime);

        Assert.AreEqual("2025-07-01", _band.Subtitle);
    }

    [TestMethod]
    public void CustomFormatModeSupportsWeekOfYearPlaceholder()
    {
        // ISO-like first week/first day settings feed the WOY placeholder
        var settings = new Settings(firstWeekOfYear: 2, firstDayOfWeek: 1, clockBandDateMode: 5, customDateFormatInClockBand: "\\W WOY");

        _band = new NowDockBand(settings, clock: () => FixedTime);

        Assert.AreEqual($"W {FixedTimeWeek()}", _band.Subtitle);
    }

    [TestMethod]
    public void CustomFormatModeWithEmptyFormatFallsBackToDefaultDate()
    {
        var settings = new Settings(clockBandDateMode: 5, customDateFormatInClockBand: string.Empty);

        _band = new NowDockBand(settings, clock: () => FixedTime);

        Assert.AreEqual("7/1/2025", _band.Subtitle);
    }

    [TestMethod]
    public void CustomFormatModeWithInvalidFormatFallsBackToDefaultDate()
    {
        // An unclosed literal quote is an invalid .NET date format
        var settings = new Settings(clockBandDateMode: 5, customDateFormatInClockBand: "'unclosed");

        _band = new NowDockBand(settings, clock: () => FixedTime);

        Assert.AreEqual("7/1/2025", _band.Subtitle);
    }

    [TestMethod]
    public void CustomFormatModeRendersUnrecognizedLettersLiterally()
    {
        // 'W' is no date format specifier and is copied to the output unchanged, so
        // a German user can write '\KW WOY' to get 'KW 27' without escaping the W.
        var settings = new Settings(firstWeekOfYear: 2, firstDayOfWeek: 1, clockBandDateMode: 5, customDateFormatInClockBand: "dd.MM \\KW WOY");

        _band = new NowDockBand(settings, clock: () => FixedTime);

        StringAssert.Contains(_band.Subtitle, "KW ");
        StringAssert.Contains(_band.Subtitle, FixedTimeWeek());
    }

    [TestMethod]
    public void ClickOpensNotificationCenterByDefault()
    {
        _band = new NowDockBand(new Settings(), clock: () => FixedTime);

        Assert.IsInstanceOfType(_band.Command, typeof(OpenUrlCommand));
    }

    [TestMethod]
    public void ClickDoesNothingWhenNotificationCenterSettingIsDisabled()
    {
        var settings = new Settings(clockBandOpensNotificationCenter: false);

        _band = new NowDockBand(settings, clock: () => FixedTime);

        Assert.IsInstanceOfType(_band.Command, typeof(NoOpCommand));
    }

    [TestMethod]
    public void CopyWeekNumberCommandHoldsWeekNumber()
    {
        var settings = new Settings(clockBandDateMode: 1);

        _band = new NowDockBand(settings, clock: () => FixedTime);

        var copyWeekItem = _band.MoreCommands[2] as CommandContextItem;
        Assert.IsNotNull(copyWeekItem);
        var copyWeekCommand = copyWeekItem.Command as CopyTextCommand;
        Assert.IsNotNull(copyWeekCommand);
        Assert.AreEqual(FixedTimeWeek(), copyWeekCommand.Text);
    }
}
