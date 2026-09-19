// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using ManagedCommon;

namespace Microsoft.CmdPal.Common.Helpers;

/// <summary>
/// Applies PowerToys' General application language to Command Palette (WinUI resources and .NET cultures).
/// </summary>
public static class AppLanguageHelper
{
    /// <summary>
    /// Gets the language tag currently applied from PowerToys settings, or empty for Windows default.
    /// </summary>
    public static string LanguageOverride { get; private set; } = string.Empty;

    /// <summary>
    /// Reads <c>language.json</c> and applies it to WinUI and .NET UI culture.
    /// Call as early as possible during process startup, before XAML is initialized.
    /// </summary>
    public static void ApplyFromPowerToysSettings()
    {
        Apply(LanguageHelper.LoadLanguage());
    }

    /// <summary>
    /// Applies a BCP-47 language tag. An empty tag means Windows default and clears a previous WinUI override.
    /// </summary>
    internal static void Apply(string? languageTag)
    {
        if (string.IsNullOrEmpty(languageTag))
        {
            LanguageOverride = string.Empty;
            TrySetWinUiLanguageOverride(string.Empty);
            return;
        }

        LanguageOverride = languageTag;
        TrySetWinUiLanguageOverride(languageTag);
        TrySetDotNetCulture(languageTag);
    }

    private static void TrySetWinUiLanguageOverride(string languageTag)
    {
        try
        {
            Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = languageTag;
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to set WinUI primary language override", ex);
        }
    }

    private static void TrySetDotNetCulture(string languageTag)
    {
        try
        {
            var culture = CultureInfo.GetCultureInfo(languageTag);
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }
        catch (CultureNotFoundException ex)
        {
            Logger.LogError($"Unknown application language tag '{languageTag}'", ex);
        }
    }
}
