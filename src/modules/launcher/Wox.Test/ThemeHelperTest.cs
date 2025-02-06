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
    private const string ThemesKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes";
    private const string PersonalizeKey = ThemesKey + "\\Personalize";
    private const string TestHighContrastThemePath = @"C:\WINDOWS\resources\Ease of Access Themes\hcwhite.theme";
    private const Theme TestHighContrastTheme = Theme.HighContrastWhite;

    private static readonly Expression<Func<IRegistryService, object>> _getAppsUseLightThemeFn = (service) =>
        service.GetValue(PersonalizeKey, "AppsUseLightTheme", 1);

    private static readonly Expression<Func<IRegistryService, object>> _getCurrentThemeFn = (service) =>
        service.GetValue(ThemesKey, "CurrentTheme", string.Empty);

    [TestMethod]
    public void When_AppsUseLightTheme_RegistryValueIs1TheLightThemeIsReturned()
    {
        var mockService = new Mock<IRegistryService>();

        mockService
            .Setup(_getAppsUseLightThemeFn)
            .Returns(1);
        var helper = new ThemeHelper(mockService.Object);

        Assert.AreEqual(Theme.Light, helper.GetAppsTheme());
    }

    [TestMethod]
    public void When_AppsUseLightTheme_RegistryValueIs0TheDarkThemeIsReturned()
    {
        var mockService = new Mock<IRegistryService>();

        mockService
            .Setup(_getAppsUseLightThemeFn)
            .Returns(0);
        var helper = new ThemeHelper(mockService.Object);

        Assert.AreEqual(Theme.Dark, helper.GetAppsTheme());
    }

    [TestMethod]
    public void When_AppsUseLightTheme_RegistryValueIsOutOfRangeTheLightThemeIsReturned()
    {
        var mockService = new Mock<IRegistryService>();

        // Only 0 and 1 are valid values for AppsUseLightTheme.
        mockService
            .Setup(_getAppsUseLightThemeFn)
            .Returns(2);
        var helper = new ThemeHelper(mockService.Object);

        Assert.AreEqual(Theme.Light, helper.GetAppsTheme());
    }

    [TestMethod]
    public void When_AppsUseLightTheme_RegistryValueIsNullTheLightThemeIsReturned()
    {
        var mockService = new Mock<IRegistryService>();

        mockService
            .Setup(_getAppsUseLightThemeFn)
            .Returns(null);
        var helper = new ThemeHelper(mockService.Object);

        Assert.AreEqual(Theme.Light, helper.GetAppsTheme());
    }

    [TestMethod]
    public void When_AppsUseLightTheme_RegistryValueIsMissingTheLightThemeIsReturned()
    {
        var mockService = new Mock<IRegistryService>();

        // When valueName does not exist, the default value is returned.
        // https://learn.microsoft.com/en-us/dotnet/api/microsoft.win32.registry.getvalue?view=net-9.0
        mockService
            .Setup(_getAppsUseLightThemeFn)
            .Returns(1);
        var helper = new ThemeHelper(mockService.Object);

        Assert.AreEqual(Theme.Light, helper.GetAppsTheme());
    }

    [TestMethod]
    public void When_AppsUseLightTheme_RegistryValueIsAStringTheLightThemeIsReturned()
    {
        var mockService = new Mock<IRegistryService>();
        mockService
            .Setup(_getAppsUseLightThemeFn)
            .Returns("RandomString");
        var helper = new ThemeHelper(mockService.Object);

        Assert.AreEqual(Theme.Light, helper.GetAppsTheme());
    }

    [TestMethod]
    public void WhenPersonalizeRegistryKeyIsMissingTheLightThemeIsReturned()
    {
        var mockService = new Mock<IRegistryService>();

        // When the registry key itself is missing, null is returned.
        // https://learn.microsoft.com/en-us/dotnet/api/microsoft.win32.registry.getvalue?view=net-9.0
        mockService
            .Setup(_getAppsUseLightThemeFn)
            .Returns(null);
        var helper = new ThemeHelper(mockService.Object);

        Assert.AreEqual(Theme.Light, helper.GetAppsTheme());
    }

    // GetHighContrastTheme tests
    [TestMethod]
    public void WhenHighContrastThemeIsActiveTheMatchingThemeIsReturned()
    {
        var mockService = new Mock<IRegistryService>();
        mockService
            .Setup(_getCurrentThemeFn)
            .Returns(TestHighContrastThemePath);
        var helper = new ThemeHelper(mockService.Object);
        Assert.AreEqual(TestHighContrastTheme, helper.GetHighContrastTheme());
    }

    [TestMethod]
    public void WhenUnknownHighContrastThemeIsSetNullIsReturned()
    {
        var mockService = new Mock<IRegistryService>();
        mockService
            .Setup(_getCurrentThemeFn)
            .Returns(@"C:\WINDOWS\resources\Ease of Access Themes\unknown.theme");
        var helper = new ThemeHelper(mockService.Object);
        Assert.IsNull(helper.GetHighContrastTheme());
    }

    [TestMethod]
    public void WhenCurrentThemeRegistryValueIsMissingNullIsReturned()
    {
        var mockService = new Mock<IRegistryService>();
        mockService
            .Setup(_getCurrentThemeFn)
            .Returns(null);
        var helper = new ThemeHelper(mockService.Object);
        Assert.IsNull(helper.GetHighContrastTheme());
    }

    [TestMethod]
    public void WhenCurrentThemeIsBlankNullIsReturned()
    {
        var mockService = new Mock<IRegistryService>();
        mockService
            .Setup(_getCurrentThemeFn)
            .Returns(string.Empty);
        var helper = new ThemeHelper(mockService.Object);
        Assert.IsNull(helper.GetHighContrastTheme());
    }

    // GetCurrentTheme tests
    [TestMethod]
    public void WhenHighContrastThemeIsNotActiveAppsThemeIsReturned()
    {
        var mockService = new Mock<IRegistryService>();
        mockService
            .Setup(_getCurrentThemeFn)
            .Returns(@"C:\WINDOWS\resources\Ease of Access Themes\random.theme");
        mockService
            .Setup(_getAppsUseLightThemeFn)
            .Returns(1);
        var helper = new ThemeHelper(mockService.Object);
        Assert.AreEqual(Theme.Light, helper.GetCurrentTheme());
    }
}
