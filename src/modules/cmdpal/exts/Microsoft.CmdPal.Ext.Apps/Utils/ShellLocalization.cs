// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Collections.Concurrent;
using System.IO;
using static Microsoft.CmdPal.Ext.Apps.Utils.Native;

namespace Microsoft.CmdPal.Ext.Apps.Utils;

/// <summary>
/// Class to get localized name of shell items like 'My computer'. The localization is based on the 'windows display language'.
/// </summary>
public class ShellLocalization
{
    internal static readonly ShellLocalization Instance = new();

    // Cache for already localized names. This makes localization of already localized string faster.
    private ConcurrentDictionary<string, string> _localizationCache = new ConcurrentDictionary<string, string>();

    /// <summary>
    /// Returns the localized name of a shell item.
    /// </summary>
    /// <param name="path">Path to the shell item (e. g. shortcut 'File Explorer.lnk').</param>
    /// <returns>The localized name as string or <see cref="string.Empty"/>.</returns>
    public string GetLocalizedName(string path)
    {
        var lowerInvariantPath = path.ToLowerInvariant();

        // Checking cache if path is already localized
        if (_localizationCache.TryGetValue(lowerInvariantPath, out var value))
        {
            return value;
        }

        var shellItemType = ShellItemTypeConstants.ShellItemGuid;
        var retCode = SHCreateItemFromParsingName(path, nint.Zero, ref shellItemType, out var shellItem);
        if (retCode != 0)
        {
            return string.Empty;
        }

        shellItem.GetDisplayName(SIGDN.NORMALDISPLAY, out var filename);

        _ = _localizationCache.TryAdd(lowerInvariantPath, filename);

        return filename;
    }

    /// <summary>
    /// This method returns the localized path to a shell item (folder or file)
    /// </summary>
    /// <param name="path">The path to localize</param>
    /// <returns>The localized path or the original path if localized version is not available</returns>
    public string GetLocalizedPath(string path)
    {
        path = Environment.ExpandEnvironmentVariables(path);
        var ext = Path.GetExtension(path);
        var pathParts = path.Split("\\");
        var locPath = new string[pathParts.Length];

        for (var i = 0; i < pathParts.Length; i++)
        {
            if (i == 0 && pathParts[i].EndsWith(':'))
            {
                // Skip the drive letter.
                locPath[0] = pathParts[0];
                continue;
            }

            // Localize path.
            var iElements = i + 1;
            var lName = GetLocalizedName(string.Join("\\", pathParts[..iElements]));
            locPath[i] = !string.IsNullOrEmpty(lName) ? lName : pathParts[i];
        }

        var newPath = string.Join("\\", locPath);
        newPath = !newPath.EndsWith(ext, StringComparison.InvariantCultureIgnoreCase) ? newPath + ext : newPath;

        return newPath;
    }
}
