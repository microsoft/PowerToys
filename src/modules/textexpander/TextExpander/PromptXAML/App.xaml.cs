// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// The XAML application behind the {{prompt}} window. It runs on <see cref="PromptHost"/>'s thread
/// and shows nothing of its own; the engine itself is not a XAML app.
/// </summary>
public partial class PromptApp : Application
{
    public PromptApp()
    {
        // The prompt thread stays up between prompts, because Application.Start can't run twice in
        // one process. Without this, closing the first prompt window would end it.
        DispatcherShutdownMode = DispatcherShutdownMode.OnExplicitShutdown;

#if DEBUG
        // Lets a developer check the window in both themes without changing Windows'. Otherwise it
        // follows Windows' theme, as PowerToys' other utilities do.
        ThemeOverride = Environment.GetEnvironmentVariable("POWERTOYS_TEXTEXPANDER_PROMPT_THEME") switch
        {
            "Light" => ApplicationTheme.Light,
            "Dark" => ApplicationTheme.Dark,
            _ => null,
        };

        if (ThemeOverride is { } theme)
        {
            RequestedTheme = theme;
        }
#endif

        InitializeComponent();
    }

    /// <summary>Gets the theme a debug build was told to use instead of Windows', or null.</summary>
    internal static ApplicationTheme? ThemeOverride { get; private set; }
}
