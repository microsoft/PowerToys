// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using ManagedCommon;

namespace RobocopyUI.Helpers;

/// <summary>
/// Remembers whether the user last used Simple or Advanced mode.
/// </summary>
internal static class SimpleModeSettings
{
    private const string RelativePath = @"Microsoft\PowerToys\RobocopyUI\ui-mode.txt";
    private const string AdvancedValue = "Advanced";

    /// <summary>
    /// Returns true when Simple mode should be shown. Missing or unreadable state defaults to Simple.
    /// </summary>
    public static bool GetIsSimpleMode()
    {
        try
        {
            var path = GetPath();
            if (!File.Exists(path))
            {
                return true;
            }

            var text = File.ReadAllText(path).Trim();
            return !text.Equals(AdvancedValue, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Failed to read RobocopyUI mode setting: {ex.Message}");
            return true;
        }
    }

    /// <summary>
    /// Persists the current mode.
    /// </summary>
    public static void SetIsSimpleMode(bool isSimple)
    {
        try
        {
            var path = GetPath();
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, isSimple ? "Simple" : AdvancedValue);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Failed to save RobocopyUI mode setting: {ex.Message}");
        }
    }

    private static string GetPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), RelativePath);
}
