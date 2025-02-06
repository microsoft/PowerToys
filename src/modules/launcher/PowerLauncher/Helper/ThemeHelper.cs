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

public class ThemeHelper
{
    private const string ThemesKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes";
    private const string PersonalizeKey = ThemesKey + "\\Personalize";

    private readonly IRegistryService _registryService;
    private readonly Dictionary<string, Theme> _highContrastThemeMap =
        new(StringComparer.InvariantCultureIgnoreCase)
    {
        { "hc1", Theme.HighContrastOne },
        { "hc2", Theme.HighContrastTwo },
        { "hcwhite", Theme.HighContrastWhite },
        { "hcblack", Theme.HighContrastBlack },
    };

    public ThemeHelper(IRegistryService registryService = null)
    {
        _registryService = registryService ?? RegistryServiceFactory.Create();
    }

    /// <summary>
    /// Determines the current theme, giving priority to an active High Contrast theme if available.
    /// </summary>
    /// <returns>A <see cref="Theme"/> value representing the active High Contrast theme if set;
    /// otherwise the current application theme (Light or Dark).</returns>
    public Theme GetCurrentTheme() => GetHighContrastTheme() ?? GetAppsTheme();

    /// <summary>
    /// Resolves the <see cref="Theme"/> that corresponds to the currently active High Contrast
    /// mode, if any.
    /// </summary>
    /// <returns>A <see cref="Theme"/> value representing the active High Contrast theme (e.g.
    /// HighContrastOne), or <c>null</c> if no High Contrast theme is active or recognized.
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
            // Fall through.
        }

        return null;
    }

    internal Theme GetAppsTheme()
    {
        try
        {
            // A 0 for this registry value represents Dark mode. If the value could not be
            // retrieved or is the wrong type, we default to Light mode.
            var regValue = _registryService.GetValue(PersonalizeKey, "AppsUseLightTheme", 1);
            return regValue is int intValue && intValue == 0 ? Theme.Dark : Theme.Light;
        }
        catch
        {
            return Theme.Light;
        }
    }
}
