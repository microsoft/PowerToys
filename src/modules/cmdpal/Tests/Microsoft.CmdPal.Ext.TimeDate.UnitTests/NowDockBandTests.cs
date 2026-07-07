// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using Microsoft.CmdPal.Ext.TimeDate;
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

    [TestMethod]
    public void Constructor_TitleIsSetImmediately()
    {
        _band = new NowDockBand(clock: () => FixedTime);

        Assert.AreEqual("2:05 PM", _band.Title);
        Assert.IsFalse(string.IsNullOrEmpty(_band.Subtitle));
    }

    [TestMethod]
    public void UpdateText_LongTimeFormat_TitleContainsSeconds()
    {
        _band = new NowDockBand(timeWithSeconds: true, clock: () => FixedTime);

        _band.UpdateText();

        Assert.AreEqual("2:05:32 PM", _band.Title);
    }

    [TestMethod]
    public void UpdateText_ShortDateFormat_SubtitleIsShortDate()
    {
        _band = new NowDockBand(clock: () => FixedTime);

        _band.UpdateText();

        Assert.AreEqual("7/1/2025", _band.Subtitle);
    }

    [TestMethod]
    public void UpdateText_FiresOnUpdatedCallback()
    {
        var callbackFired = false;
        _band = new NowDockBand(onUpdated: () => callbackFired = true, clock: () => FixedTime);

        callbackFired = false; // reset — constructor already fired it once during synchronous UpdateText()

        _band.UpdateText();

        Assert.IsTrue(callbackFired);
    }

    [TestMethod]
    public void UpdateText_CallbackFiredAfterAssignments()
    {
        var titleAtCallback = string.Empty;
        _band = new NowDockBand(
            onUpdated: () => titleAtCallback = _band?.Title ?? string.Empty,
            clock: () => FixedTime);

        titleAtCallback = string.Empty; // reset after construction callback

        _band.UpdateText();

        Assert.IsFalse(string.IsNullOrEmpty(titleAtCallback), "Title should be assigned before callback fires");
    }

    [TestMethod]
    public void UpdateText_CopyCommandsUpdated()
    {
        _band = new NowDockBand(clock: () => FixedTime);

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

        _band = new NowDockBand(clock: () => FixedTime);

        Assert.IsFalse(string.IsNullOrEmpty(_band.Title), $"Title should be non-empty for culture '{cultureName}'");
        Assert.IsFalse(string.IsNullOrEmpty(_band.Subtitle), $"Subtitle should be non-empty for culture '{cultureName}'");
    }

    [TestMethod]
    public void UpdateSettings_EnablingSeconds_TitleIncludesSeconds()
    {
        _band = new NowDockBand(timeWithSeconds: false, clock: () => FixedTime);

        Assert.AreEqual("2:05 PM", _band.Title, "Precondition: seconds hidden by default");

        _band.UpdateSettings(timeWithSeconds: true);

        Assert.AreEqual("2:05:32 PM", _band.Title, "Title should update live to include seconds");
    }

    [TestMethod]
    public void UpdateSettings_DisablingSeconds_TitleDropsSeconds()
    {
        _band = new NowDockBand(timeWithSeconds: true, clock: () => FixedTime);

        Assert.AreEqual("2:05:32 PM", _band.Title, "Precondition: seconds shown");

        _band.UpdateSettings(timeWithSeconds: false);

        Assert.AreEqual("2:05 PM", _band.Title, "Title should update live to drop seconds");
    }

    [TestMethod]
    public void UpdateSettings_NoChange_FiresNoCallback()
    {
        var callbackCount = 0;
        _band = new NowDockBand(timeWithSeconds: false, onUpdated: () => callbackCount++, clock: () => FixedTime);

        callbackCount = 0; // reset after construction callback

        _band.UpdateSettings(timeWithSeconds: false);

        Assert.AreEqual(0, callbackCount, "A no-op settings change should not refresh the band");
    }
}
