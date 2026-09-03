// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using Microsoft.CmdPal.Ext.TimeDate;
using Microsoft.CmdPal.Ext.TimeDate.Helpers;
using Microsoft.CmdPal.Ext.TimeDate.Pages;
using Microsoft.CommandPalette.Extensions;
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
    private ClockUpdateService _clockUpdateService = null!;
    private NowDockBand? _band;

    [TestInitialize]
    public void Setup()
    {
        _originalCulture = CultureInfo.CurrentCulture;
        _originalUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = new CultureInfo("en-US", false);
        CultureInfo.CurrentUICulture = new CultureInfo("en-US", false);
        _clockUpdateService = new ClockUpdateService(() => FixedTime, enableTimer: false);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _band?.Dispose();
        _band = null;
        _clockUpdateService.Dispose();
        CultureInfo.CurrentCulture = _originalCulture;
        CultureInfo.CurrentUICulture = _originalUiCulture;
    }

    [TestMethod]
    public void Constructor_TitleIsSetImmediately()
    {
        _band = CreateBand();

        Assert.AreEqual("2:05 PM", _band.Title);
        Assert.IsFalse(string.IsNullOrEmpty(_band.Subtitle));
    }

    [TestMethod]
    public void UpdateText_LongTimeFormat_TitleContainsSeconds()
    {
        _band = CreateBand(titleFormat: "T");

        _band.UpdateText();

        Assert.AreEqual("2:05:32 PM", _band.Title);
    }

    [TestMethod]
    public void UpdateText_ShortDateFormat_SubtitleIsShortDate()
    {
        _band = CreateBand();

        _band.UpdateText();

        Assert.AreEqual("7/1/2025", _band.Subtitle);
    }

    [TestMethod]
    public void UpdateText_CopyCommandsUpdated()
    {
        _band = CreateBand();

        _band.UpdateText();

        Assert.AreEqual(_band.Title, _band.CopyTitleCommand.Text);
        Assert.AreEqual(_band.Subtitle, _band.CopySubtitleCommand.Text);
        StringAssert.StartsWith(_band.CopyTitleCommand.Name, "Copy time (");
        StringAssert.StartsWith(_band.CopySubtitleCommand.Name, "Copy date (");
    }

    [TestMethod]
    public void Constructor_OptionalCopyFormatAddsCurrentFormattedCopyCommand()
    {
        _band = CreateBand(copyFormat: "s");

        Assert.IsNotNull(_band.CopyCustomFormatCommand);
        Assert.AreEqual("2025-07-01T14:05:32", _band.CopyCustomFormatCommand.GetCurrentText());
        StringAssert.StartsWith(_band.CopyCustomFormatCommand.Name, "Copy ISO 8601");
    }

    [TestMethod]
    public void Constructor_NoCopyFormatOmitsCustomCopyCommand()
    {
        _band = CreateBand();

        Assert.IsNull(_band.CopyCustomFormatCommand);
    }

    [TestMethod]
    public void UpdateSettings_AddsAndRemovesOptionalCopyCommand()
    {
        var settings = new TestDockClockSettings();
        _band = new NowDockBand(settings, new NoOpCommand(), _clockUpdateService, () => FixedTime);

        settings.SetDockClockFormats("t", "d", "s");
        _band.UpdateSettings(settings);
        Assert.IsNotNull(_band.CopyCustomFormatCommand);

        settings.SetDockClockFormats("t", "d", string.Empty);
        _band.UpdateSettings(settings);
        Assert.IsNull(_band.CopyCustomFormatCommand);
    }

    [TestMethod]
    public void Tick_ReadsClockOnce()
    {
        var clockReads = 0;
        _band = CreateBand(
            titleFormat: "T",
            clock: () =>
            {
                clockReads++;
                return FixedTime;
            });
        _band.StartUpdating();
        var readsBeforeTick = clockReads;

        _clockUpdateService.DispatchTick(FixedTime.AddSeconds(1));

        Assert.AreEqual(readsBeforeTick + 1, clockReads);
    }

    [TestMethod]
    public void DisposeDuringStartDoesNotLeaveSubscription()
    {
        var clockReads = 0;
        var disposeOnRead = false;

        // This single-threaded reentrancy covers Subscribe/UpdateText ordering;
        // _lifecycleLock is still required when StartUpdating and Dispose run on different threads
        _band = CreateBand(
            titleFormat: "T",
            clock: () =>
            {
                clockReads++;
                if (disposeOnRead)
                {
                    disposeOnRead = false;
                    _band!.Dispose();
                }

                return FixedTime;
            });
        disposeOnRead = true;

        _band.StartUpdating();
        var readsAfterStart = clockReads;
        _clockUpdateService.DispatchTick(FixedTime.AddSeconds(1));

        Assert.AreEqual(readsAfterStart, clockReads);
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

        _band = CreateBand();

        Assert.IsFalse(string.IsNullOrEmpty(_band.Title), $"Title should be non-empty for culture '{cultureName}'");
        Assert.IsFalse(string.IsNullOrEmpty(_band.Subtitle), $"Subtitle should be non-empty for culture '{cultureName}'");
    }

    [TestMethod]
    public void UpdateSettings_EnablingSeconds_TitleIncludesSeconds()
    {
        var settings = new TestDockClockSettings();
        _band = new NowDockBand(settings, new NoOpCommand(), _clockUpdateService, clock: () => FixedTime);

        Assert.AreEqual("2:05 PM", _band.Title, "Precondition: seconds hidden by default");

        settings.SetDockClockFormats("T", "d", string.Empty);
        _band.UpdateSettings(settings);

        Assert.AreEqual("2:05:32 PM", _band.Title, "Title should update live to include seconds");
    }

    [TestMethod]
    public void UpdateSettings_DisablingSeconds_TitleDropsSeconds()
    {
        var settings = new TestDockClockSettings(titleFormat: "T");
        _band = new NowDockBand(settings, new NoOpCommand(), _clockUpdateService, clock: () => FixedTime);

        Assert.AreEqual("2:05:32 PM", _band.Title, "Precondition: seconds shown");

        settings.SetDockClockFormats("t", "d", string.Empty);
        _band.UpdateSettings(settings);

        Assert.AreEqual("2:05 PM", _band.Title, "Title should update live to drop seconds");
    }

    [TestMethod]
    public void UpdateSettings_WeekRulesRefreshFormattedTitleSubtitleAndCopyText()
    {
        var time = new DateTime(2012, 12, 31, 14, 5, 32);
        var settings = new TestDockClockSettings("WOY", "IWYR-\\WIWOY-IDOW", "\\WWOY")
        {
            FirstWeekOfYear = 0,
            FirstDayOfWeek = 0,
        };
        _band = new NowDockBand(settings, new NoOpCommand(), _clockUpdateService, () => time);

        Assert.AreEqual("53", _band.Title);
        Assert.AreEqual("2013-W01-1", _band.Subtitle);
        Assert.IsNotNull(_band.CopyCustomFormatCommand);
        Assert.AreEqual("W53", _band.CopyCustomFormatCommand.GetCurrentText());

        settings.FirstWeekOfYear = 2;
        settings.FirstDayOfWeek = 1;
        _band.UpdateSettings(settings);

        Assert.AreEqual("1", _band.Title);
        Assert.AreEqual("2013-W01-1", _band.Subtitle);
        Assert.AreEqual(_band.Title, _band.CopyTitleCommand.Text);
        Assert.AreEqual(_band.Subtitle, _band.CopySubtitleCommand.Text);
        Assert.IsNotNull(_band.CopyCustomFormatCommand);
        Assert.AreEqual("W1", _band.CopyCustomFormatCommand.GetCurrentText());
    }

    [TestMethod]
    public void ClockTicks_ReachTheBandOnlyBetweenStartAndStopUpdating()
    {
        var now = FixedTime;
        _band = CreateBand(titleFormat: "T", clock: () => now);

        Assert.AreEqual("2:05:32 PM", _band.Title, "Precondition: the title is set at construction");

        // Bands are constructed for every clock, pinned or not, so an unrendered
        // band must stay off the shared clock cadence.
        now = FixedTime.AddSeconds(5);
        _clockUpdateService.DispatchTick(now);
        Assert.AreEqual("2:05:32 PM", _band.Title, "An unrendered band should ignore clock ticks");

        _band.StartUpdating();
        Assert.AreEqual("2:05:37 PM", _band.Title, "StartUpdating should refresh the stale title immediately");

        now = FixedTime.AddSeconds(9);
        _clockUpdateService.DispatchTick(now);
        Assert.AreEqual("2:05:41 PM", _band.Title, "A rendered band should follow clock ticks");

        _band.StopUpdating();
        now = FixedTime.AddSeconds(20);
        _clockUpdateService.DispatchTick(now);
        Assert.AreEqual("2:05:41 PM", _band.Title, "A band should stop ticking once it is no longer rendered");
    }

    [TestMethod]
    public void OnLoadDockBandItem_RenderingTheBandDrivesItsSubscription()
    {
        var now = FixedTime;
        _band = CreateBand(titleFormat: "T", clock: () => now);
        var bandItem = new OnLoadDockBandItem([_band], "test.band", "Test band", _band.StartUpdating, _band.StopUpdating);
        var page = (IListPage)bandItem.Command!;

        static void Handler(object sender, IItemsChangedEventArgs args)
        {
        }

        // CmdPal signals that a band is on screen purely by attaching and
        // detaching an items-changed handler on the band's page.
        now = FixedTime.AddSeconds(5);
        page.ItemsChanged += Handler;
        Assert.AreEqual("2:05:37 PM", _band.Title, "Rendering the band should start its updates");

        now = FixedTime.AddSeconds(9);
        _clockUpdateService.DispatchTick(now);
        Assert.AreEqual("2:05:41 PM", _band.Title, "A rendered band should follow clock ticks");

        page.ItemsChanged -= Handler;
        now = FixedTime.AddSeconds(20);
        _clockUpdateService.DispatchTick(now);
        Assert.AreEqual("2:05:41 PM", _band.Title, "Dropping the band should stop its updates");
    }

    private NowDockBand CreateBand(string titleFormat = "t", string copyFormat = "", Func<DateTime>? clock = null)
    {
        var settings = new TestDockClockSettings(titleFormat, copyFormat: copyFormat);
        return new NowDockBand(settings, new NoOpCommand(), _clockUpdateService, clock ?? (() => FixedTime));
    }

    private sealed class TestDockClockSettings(string titleFormat = "t", string subtitleFormat = "d", string copyFormat = "") : Settings, IDockClockSettings
    {
        public string DockClockTitleFormat { get; private set; } = titleFormat;

        public string DockClockSubtitleFormat { get; private set; } = subtitleFormat;

        public string DockClockCopyFormat { get; private set; } = copyFormat;

        public string DockClockClickAction => "default";

        public void SetDockClockFormats(string titleFormat, string subtitleFormat, string copyFormat)
        {
            DockClockTitleFormat = titleFormat;
            DockClockSubtitleFormat = subtitleFormat;
            DockClockCopyFormat = copyFormat;
        }
    }
}
