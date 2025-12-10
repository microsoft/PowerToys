// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Services;

/// <summary>
/// Provides theme-related values for the Command Palette and notifies listeners about
/// changes that affect visual appearance (theme, tint, background image, and backdrop).
/// </summary>
/// <remarks>
/// Implementations are expected to monitor system/app theme changes and raise
/// <see cref="ThemeChanged"/> accordingly. Consumers should call <see cref="Initialize"/>
/// once to hook required sources and then query properties/methods for the current visuals.
/// </remarks>
public interface IThemeService
{
    /// <summary>
    /// Occurs when the effective theme or any visual-affecting setting changes.
    /// </summary>
    /// <remarks>
    /// Triggered for changes such as app theme (light/dark/default), background image,
    /// tint/accent, or backdrop parameters that would require UI to refresh styling.
    /// </remarks>
    event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

    /// <summary>
    /// Initializes the theme service and starts listening for theme-related changes.
    /// </summary>
    /// <remarks>
    /// Safe to call once during application startup before consuming the service.
    /// </remarks>
    void Initialize();

    /// <summary>
    /// Gets the current theme settings.
    /// </summary>
    ThemeSnapshot Current { get; }
}
