// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using ManagedCommon;

namespace Microsoft.CmdPal.Ext.Apps.Utils;

/// <summary>
/// Helper class to locate and invoke the PowerToys ActionRunner executable.
/// ActionRunner is used to work around WinUI3/MSIX packaging limitations where
/// certain shell operations (like "Run as different user" or "Run as administrator")
/// don't work properly from within a packaged app context.
/// </summary>
internal static class ActionRunnerHelper
{
    private const string ActionRunnerExeName = "PowerToys.ActionRunner.exe";

    private static string? _cachedPath;

    /// <summary>
    /// Gets the path to the ActionRunner executable.
    /// </summary>
    /// <returns>The full path to ActionRunner.exe, or null if not found.</returns>
    public static string? GetActionRunnerPath()
    {
        if (_cachedPath != null)
        {
            return _cachedPath;
        }

        _cachedPath = FindActionRunnerPath();
        return _cachedPath;
    }

    private static string? FindActionRunnerPath()
    {
        // Use the standard PowerToys path resolver to find the installation directory.
        // This handles registry lookups for installed versions and debug builds correctly.
        var installPath = PowerToysPathResolver.GetPowerToysInstallPath();
        if (!string.IsNullOrEmpty(installPath))
        {
            var actionRunnerPath = Path.Combine(installPath, ActionRunnerExeName);
            if (File.Exists(actionRunnerPath))
            {
                return actionRunnerPath;
            }
        }

        return null;
    }
}
