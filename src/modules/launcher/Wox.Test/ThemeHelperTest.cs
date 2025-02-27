// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq.Expressions;
using ManagedCommon;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PowerLauncher.Helper;
using PowerLauncher.Services;

namespace Wox.Test;

[TestClass]
public class ThemeHelperTest
{
    // Registry key paths.
    private const string ThemesKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes";
    private const string PersonalizeKey = ThemesKey + "\\Personalize";

    // Theme paths.
    private const string HighContrastThemePath = @"C:\WINDOWS\resources\Ease of Access Themes\hcwhite.theme";
    private const string NonHighContrastThemePath = @"C:\Users\Test\AppData\Local\Microsoft\Windows\Themes\Custom.theme";

    /// <summary>
    /// The expected High Contrast theme when the <see cref="HighContrastThemePath"/> is returned
    /// from the registry.
    /// </summary>
    private const Theme HighContrastTheme = Theme.HighContrastWhite;

    /// <summary>
    /// Mock <see cref="IRegistryService.GetValue"/>, to return the value of the AppsUseLightTheme
    /// key.
    /// </summary>
    private static readonly Expression<Func<IRegistryService, object>> _mockAppsUseLightTheme = (service) =>
        service.GetValue(PersonalizeKey, "AppsUseLightTheme", ThemeHelper.AppsUseLightThemeDefault);

    /// <summary>
    /// Mock <see cref="IRegistryService.GetValue"/> to return the value of the CurrentTheme key.
    /// </summary>
    /// <remarks>
    /// The default value given here - string.Empty - must be the same as the default value in the
    /// actual code for tests using this mock to be valid.
    /// </remarks>
    private static readonly Expression<Func<IRegistryService, object>> _mockCurrentTheme = (service) =>
        service.GetValue(ThemesKey, "CurrentTheme", string.Empty);

    /// <summary>
    /// Test GetAppsTheme method.
    /// </summary>
    /// <param name="registryValue">The mocked value for the AppsUseLightTheme registry key.</param>
    /// <param name="expectedTheme">The expected <see cref="Theme"/> output from the call to
    /// <see cref="ThemeHelper.GetAppsTheme"/>.</param>
    [DataTestMethod]
    [DataRow(ThemeHelper.AppsUseLightThemeLight, Theme.Light)]
    [DataRow(ThemeHelper.AppsUseLightThemeDark, Theme.Dark)]
    [DataRow(int.MaxValue, Theme.Light)] // Out of range values should default to Light
    [DataRow(null, Theme.Light)] // Missing keys or values should default to Light
    [DataRow("RandomString", Theme.Light)] // Invalid string values should default to Light
    public void GetAppsTheme_ReturnsExpectedTheme(object registryValue, Theme expectedTheme)
    {
        var mockService = new Mock<IRegistryService>();
        mockService.Setup(_mockAppsUseLightTheme).Returns(registryValue);

        var helper = new ThemeHelper(mockService.Object);

        Assert.AreEqual(expectedTheme, helper.GetAppsTheme());
    }

    /// <summary>
    /// Test <see cref="ThemeHelper.GetHighContrastTheme"/>.
    /// </summary>
    /// <param name="registryValue">The mocked value for the CurrentTheme registry key.</param>
    /// <param name="expectedTheme">The expected <see cref="Theme"/> output from the call to
    /// <see cref="ThemeHelper.GetHighContrastTheme"/>.</param>
    [DataTestMethod]
    [DataRow(HighContrastThemePath, HighContrastTheme)] // Valid High Contrast theme
    [DataRow(NonHighContrastThemePath, null)] // Non-High Contrast theme should return null
    [DataRow(null, null)] // Missing keys or values should default to null
    [DataRow("", null)] // Empty string values should default to null
    public void GetHighContrastTheme_ReturnsExpectedTheme(string registryValue, Theme? expectedTheme)
    {
        var mockService = new Mock<IRegistryService>();
        mockService.Setup(_mockCurrentTheme).Returns(registryValue);

        var helper = new ThemeHelper(mockService.Object);

        Assert.AreEqual(expectedTheme, helper.GetHighContrastTheme());
    }

    /// <summary>
    /// Test <see cref="ThemeHelper.DetermineTheme"/>.
    /// </summary>
    /// <param name="registryTheme">The mocked value for the CurrentTheme registry key.</param>
    /// <param name="requestedTheme">The <see cref="Theme"/> value from the application's settings.
    /// </param>
    /// <param name="expectedTheme">The expected <see cref="Theme"/> output from the call to
    /// <see cref="ThemeHelper.DetermineTheme"/>.</param>
    /// <param name="appsUseLightTheme">The mocked value for the AppsUseLightTheme registry key,
    /// representing the system preference for Light or Dark mode.</param>
    [DataTestMethod]
    [DataRow(HighContrastThemePath, Theme.System, HighContrastTheme)] // High Contrast theme active
    [DataRow(HighContrastThemePath, Theme.Light, HighContrastTheme)] // High Contrast theme active - Light mode override ignored
    [DataRow(HighContrastThemePath, Theme.Dark, HighContrastTheme)] // High Contrast theme active - Dark mode override ignored
    [DataRow(NonHighContrastThemePath, Theme.System, Theme.Light)] // System preference with default light theme
    [DataRow(NonHighContrastThemePath, Theme.System, Theme.Dark, ThemeHelper.AppsUseLightThemeDark)] // System preference with dark mode
    [DataRow(NonHighContrastThemePath, Theme.Light, Theme.Light, ThemeHelper.AppsUseLightThemeDark)] // Light mode override
    [DataRow(NonHighContrastThemePath, Theme.Dark, Theme.Dark, ThemeHelper.AppsUseLightThemeLight)] // Dark mode override
    [DataRow(null, Theme.System, Theme.Light)] // Missing keys or values should default to Light
    [DataRow("", Theme.System, Theme.Light)] // Empty current theme paths should default to Light
    [DataRow("RandomString", Theme.System, Theme.Light)] // Invalid current theme paths should default to Light
    [DataRow(NonHighContrastThemePath, (Theme)int.MaxValue, Theme.Light)] // Invalid theme values should default to Light
    public void DetermineTheme_ReturnsExpectedTheme(string registryTheme, Theme requestedTheme, Theme expectedTheme, int? appsUseLightTheme = 1)
    {
        var mockService = new Mock<IRegistryService>();
        mockService.Setup(_mockCurrentTheme).Returns(registryTheme);
        mockService.Setup(_mockAppsUseLightTheme).Returns(appsUseLightTheme);

        var helper = new ThemeHelper(mockService.Object);

        Assert.AreEqual(expectedTheme, helper.DetermineTheme(requestedTheme));
    }
}
