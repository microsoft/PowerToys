// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using ManagedCommon;
using PowerLauncher.Services;

[assembly: InternalsVisibleTo("Wox.Test")]

namespace PowerLauncher.Helper;

/// <summary>
/// Provides functionality for determining the application's theme based on system settings, user
/// preferences, and High Contrast mode detection.
/// </summary>
public class ThemeHelper
{
    private const string ThemesKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes";
    private const string PersonalizeKey = ThemesKey + "\\Personalize";

    internal const int AppsUseLightThemeLight = 1;
    internal const int AppsUseLightThemeDark = 0;

    /// <summary>
    /// Default value for the "AppsUseLightTheme" registry setting. This value represents Light
    /// mode and will be used if the registry value is invalid or cannot be read.
    /// </summary>
    internal const int AppsUseLightThemeDefault = AppsUseLightThemeLight;

    private readonly IRegistryService _registryService;

    private readonly Dictionary<string, Theme> _highContrastThemeMap =
        new(StringComparer.InvariantCultureIgnoreCase)
    {
        { "hc1", Theme.HighContrastOne },
        { "hc2", Theme.HighContrastTwo },
        { "hcwhite", Theme.HighContrastWhite },
        { "hcblack", Theme.HighContrastBlack },
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="ThemeHelper"/> class.
    /// </summary>
    /// <param name="registryService">The service used to query registry values. If <c>null</c>, a
    /// default implementation is used, which queries the Windows registry. This allows for
    /// dependency injection and unit testing.</param>
    public ThemeHelper(IRegistryService registryService = null)
    {
        _registryService = registryService ?? RegistryServiceFactory.Create();
    }

    /// <summary>
    /// Determines the theme to apply, prioritizing an active High Contrast theme.
    /// </summary>
    /// <param name="settingsTheme">The theme selected in application settings.</param>
    /// <returns>The resolved <see cref="Theme"/> based on the following priority order:
    /// 1. If a default High Contrast Windows theme is active, return the corresponding High
    /// Contrast <see cref="Theme"/>.
    /// 2. If "Windows default" is selected in application settings, return the Windows app theme
    /// (<see cref="Theme.Dark"/> or <see cref="Theme.Light"/>).
    /// 3. If the user explicitly selected "Light" or "Dark", return their chosen theme.
    /// 4. If the theme cannot be determined, return <see cref="Theme.Light"/>.
    /// </returns>
    public Theme DetermineTheme(Theme settingsTheme) =>
        GetHighContrastTheme() ??
            (settingsTheme == Theme.System ? GetAppsTheme() : ValidateTheme(settingsTheme));

    /// <summary>
    /// Ensures the provided <see cref="Theme"/> value is valid.
    /// </summary>
    /// <param name="theme">The <see cref="Theme"/> value to validate.</param>
    /// <returns>The provided theme if it is a defined enum value; otherwise, defaults to
    /// <see cref="Theme.Light"/>.
    private Theme ValidateTheme(Theme theme) => Enum.IsDefined(theme) ? theme : Theme.Light;

    /// <summary>
    /// Determines if a High Contrast theme is currently active and returns the corresponding
    /// <see cref="Theme"/>.
    /// </summary>
    /// <returns>The detected High Contrast <see cref="Theme"/> (e.g.
    /// <see cref="Theme.HighContrastOne"/>, or <c>null</c> if no recognized High Contrast theme
    /// is active.
    /// </returns>
    internal Theme? GetHighContrastTheme()
    {
        try
        {
            var themePath = Convert.ToString(
                _registryService.GetValue(ThemesKey, "CurrentTheme", string.Empty),
                CultureInfo.InvariantCulture);

            if (!string.IsNullOrEmpty(themePath) && _highContrastThemeMap.TryGetValue(
                Path.GetFileNameWithoutExtension(themePath), out var theme))
            {
                return theme;
            }
        }
        catch
        {
            // Fall through to return null. Ignore exception.
        }

        return null;
    }

    /// <summary>
    /// Retrieves the Windows app theme preference from the registry.
    /// </summary>
    /// <returns><see cref="Theme.Dark"/> if the user has selected Dark mode for apps,
    /// <see cref="Theme.Light"/> otherwise. If the registry value cannot be read or is invalid,
    /// the default value (<see cref="Theme.Light"/>) is used.
    /// </returns>
    internal Theme GetAppsTheme()
    {
        try
        {
            // "AppsUseLightTheme" registry value:
            // - 0 = Dark mode
            // - 1 (or missing/invalid) = Light mode
            var regValue = _registryService.GetValue(
                PersonalizeKey,
                "AppsUseLightTheme",
                AppsUseLightThemeDefault);

            return regValue is int intValue && intValue == 0 ? Theme.Dark : Theme.Light;
        }
        catch
        {
            return Theme.Light;
        }
    }
}
