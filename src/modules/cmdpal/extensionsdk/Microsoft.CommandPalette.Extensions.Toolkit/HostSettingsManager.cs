// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
/// Provides static access to the current Command Palette host settings.
/// Extensions can use this class to read the current settings values and respond to changes.
/// </summary>
public static class HostSettingsManager
{
    private static IHostSettings? _current;

    /// <summary>
    /// Occurs when the host settings have changed.
    /// Extensions can subscribe to this event to refresh their UI when settings are updated.
    /// </summary>
    public static event Action? SettingsChanged;

    /// <summary>
    /// Gets the current host settings, or null if not yet initialized.
    /// </summary>
    public static IHostSettings? Current => _current;

    /// <summary>
    /// Gets a value indicating whether host settings are available.
    /// Returns false if the extension is running with an older host that doesn't support settings.
    /// </summary>
    public static bool IsAvailable => _current != null;

    /// <summary>
    /// Updates the cached host settings. Called internally when settings change.
    /// </summary>
    /// <param name="settings">The updated host settings.</param>
    internal static void Update(IHostSettings settings)
    {
        _current = settings;
#if DEBUG
        var subscriberCount = SettingsChanged?.GetInvocationList().Length ?? 0;
        ExtensionHost.LogMessage($"[HostSettingsManager] Update called, subscriber count: {subscriberCount}");
#endif
        SettingsChanged?.Invoke();
#if DEBUG
        ExtensionHost.LogMessage($"[HostSettingsManager] SettingsChanged event invoked");
#endif
    }
}
