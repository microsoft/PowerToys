// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.ColorPicker.UITests.Next;

/// <summary>
/// Demonstrates the winappcli-backed harness against PowerToys Settings — the Color Picker
/// dashboard toggle round-trip used to validate that the new pipeline (MSTest discovery →
/// Microsoft.Testing.Platform runner → Test Explorer / dotnet test → winapp.exe) works
/// end-to-end without WinAppDriver, Selenium, Appium, or any other third-party engine NuGet.
/// </summary>
[TestClass]
public class ColorPickerToggleTests : UITestBase
{
    public ColorPickerToggleTests()
        : base(PowerToysModule.PowerToysSettings)
    {
    }

    /// <summary>
    /// Best-effort: clear the Settings search box before each test so previous tests can't
    /// hide dashboard cards via stale filter state.
    /// </summary>
    [TestCleanup]
    public void ClearSearchBox()
    {
        try
        {
            var search = Session.FindAll<TextBox>(By.Name("Search for settings"), timeoutMS: 500);
            if (search.Count > 0)
            {
                search[0].SetText(string.Empty);
            }
        }
        catch
        {
            // Best effort only — never let cleanup mask the real test failure.
        }
    }

    [TestMethod]
    [TestCategory("ColorPicker")]
    [TestCategory("winappcli-POC")]
    public void ToggleColorPickerOnAndOff()
    {
        var toggle = Find<ToggleSwitch>("Color Picker");
        var initial = toggle.IsOn;

        toggle.Toggle(!initial);
        Assert.IsTrue(
            toggle.WaitForProperty("ToggleState", !initial ? "On" : "Off", timeoutMS: 5000),
            $"Toggle did not flip from {initial} to {!initial}");

        toggle.Toggle(initial);
        Assert.IsTrue(
            toggle.WaitForProperty("ToggleState", initial ? "On" : "Off", timeoutMS: 5000),
            $"Toggle did not return to original state ({initial})");
    }

    [TestMethod]
    [TestCategory("ColorPicker")]
    [TestCategory("winappcli-POC")]
    public void SearchBoxFiltersToColorPicker()
    {
        // The Settings search box surfaces as an Edit control named "Search for settings".
        // Typing into it filters the dashboard cards; we then assert that a Color Picker hit
        // shows up. Exercises ValuePattern via `winapp ui set-value`.
        var searchBox = Find<TextBox>(By.Name("Search for settings"));
        searchBox.SetText("color picker");

        Assert.IsTrue(
            Session.WaitFor(() => Session.Has(By.Name("Color Picker")), timeoutMS: 3000),
            "Search did not surface any 'Color Picker' result");
    }

    [TestMethod]
    [TestCategory("ColorPicker")]
    [TestCategory("winappcli-POC")]
    public void GlobalKeyboardCanEscape()
    {
        // Demonstrates the harness's global keyboard helper (keybd_event) — required for
        // PowerToys global hotkeys like Win+Shift+C. Here we just send Esc and confirm the
        // Settings window is still alive (window title resolvable) afterward.
        Session.SendKeys(Key.Esc);
        Thread.Sleep(200);
        Assert.IsFalse(string.IsNullOrEmpty(Session.WindowTitle), "Window title lost after Esc");
    }
}
