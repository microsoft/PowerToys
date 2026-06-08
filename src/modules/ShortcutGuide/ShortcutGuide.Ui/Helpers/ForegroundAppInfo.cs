// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace ShortcutGuide.Helpers
{
    /// <summary>
    /// Snapshot of the user's foreground application at Shortcut Guide startup.
    /// Captured before any Shortcut Guide window is created so that the foreground
    /// process is the user's app and not Shortcut Guide itself.
    /// </summary>
    /// <param name="ModuleName">Main module file name (for example, "msedge.exe"); null when unavailable.</param>
    /// <param name="ExecutablePath">Full path to the main module executable; null when unavailable.</param>
    public readonly record struct ForegroundAppInfo(string? ModuleName, string? ExecutablePath);
}
