// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.Common.Services;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.UI.Xaml;

namespace Microsoft.CmdPal.UI.Services;

/// <summary>
/// Synchronizes a window's theme with <see cref="IThemeService"/>.
/// </summary>
internal sealed partial class WindowThemeSynchronizer : IDisposable
{
    private readonly IThemeService _themeService;
    private readonly Window _window;

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowThemeSynchronizer"/> class and subscribes to theme changes.
    /// </summary>
    /// <param name="themeService">The theme service to monitor for changes.</param>
    /// <param name="window">The window to synchronize.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="themeService"/> or <paramref name="window"/> is null.</exception>
    public WindowThemeSynchronizer(IThemeService themeService, Window window)
    {
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _themeService.ThemeChanged += ThemeServiceOnThemeChanged;
    }

    /// <summary>
    /// Unsubscribes from theme change events.
    /// </summary>
    public void Dispose()
    {
        _themeService.ThemeChanged -= ThemeServiceOnThemeChanged;
    }

    /// <summary>
    /// Applies the current theme to the window when theme changes occur.
    /// </summary>
    private void ThemeServiceOnThemeChanged(object? sender, ThemeChangedEventArgs e)
    {
        if (_window.Content is not FrameworkElement fe)
        {
            return;
        }

        var dispatcherQueue = fe.DispatcherQueue;

        if (dispatcherQueue is not null && dispatcherQueue.HasThreadAccess)
        {
            ApplyRequestedTheme(fe);
        }
        else
        {
            dispatcherQueue?.TryEnqueue(() => ApplyRequestedTheme(fe));
        }
    }

    private void ApplyRequestedTheme(FrameworkElement fe)
    {
        // LOAD BEARING: Changing the RequestedTheme to Dark then Light then target forces
        // a refresh of the theme.
        fe.RequestedTheme = ElementTheme.Dark;
        fe.RequestedTheme = ElementTheme.Light;
        fe.RequestedTheme = _themeService.Current.Theme;
    }
}
