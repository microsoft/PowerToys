// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.PowerToys.SettingsBackupRestore.Security;

/// <summary>
/// Windows path normalization and containment rules used before handle-relative access.
/// </summary>
public static class SecurePath
{
    private const int MaximumComponentLength = 255;
    private static readonly SearchValues<char> InvalidComponentCharacters = SearchValues.Create("\0<>\"|?*:");

    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON",
        "PRN",
        "AUX",
        "NUL",
        "COM1",
        "COM2",
        "COM3",
        "COM4",
        "COM5",
        "COM6",
        "COM7",
        "COM8",
        "COM9",
        "LPT1",
        "LPT2",
        "LPT3",
        "LPT4",
        "LPT5",
        "LPT6",
        "LPT7",
        "LPT8",
        "LPT9",
    };

    /// <summary>
    /// Normalizes a Windows relative path and rejects traversal, rooted, ADS, and ambiguous names.
    /// </summary>
    public static string NormalizeRelative(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string windowsPath = path.Replace('/', '\\');
        if (Path.IsPathRooted(windowsPath) || windowsPath.StartsWith('\\'))
        {
            throw new InvalidDataException($"Rooted path is not allowed: {path}");
        }

        string[] components = windowsPath.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (components.Length == 0)
        {
            throw new InvalidDataException("An empty relative path is not allowed.");
        }

        foreach (string component in components)
        {
            ValidateComponent(component, path);
        }

        string normalized = string.Join('\\', components);
        string anchor = Path.GetFullPath(@"C:\PowerToysBackupRestoreAnchor");
        string candidate = Path.GetFullPath(Path.Combine(anchor, normalized));
        if (!IsContained(anchor, candidate) || WindowsPathComparer.Instance.EqualsPath(anchor, candidate))
        {
            throw new InvalidDataException($"Path escapes its root: {path}");
        }

        return normalized;
    }

    /// <summary>
    /// Returns whether a final handle path is equal to or below a root path using separator-safe comparison.
    /// </summary>
    public static bool IsContained(string rootPath, string candidatePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(candidatePath);

        string root = NormalizeFinalPath(rootPath);
        string candidate = NormalizeFinalPath(candidatePath);
        if (WindowsPathComparer.Instance.EqualsPath(root, candidate))
        {
            return true;
        }

        string prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        return WindowsPathComparer.Instance.StartsWith(candidate, prefix);
    }

    internal static string NormalizeFinalPath(string path)
    {
        string normalized = path;
        if (normalized.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
        {
            normalized = @"\\" + normalized[8..];
        }
        else if (normalized.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[4..];
        }

        normalized = Path.GetFullPath(normalized.Replace('/', '\\'));
        return Path.TrimEndingDirectorySeparator(normalized);
    }

    private static void ValidateComponent(string component, string originalPath)
    {
        if (component.Length > MaximumComponentLength)
        {
            throw new InvalidDataException($"Path component exceeds {MaximumComponentLength} characters: {originalPath}");
        }

        if (component is "." or "..")
        {
            throw new InvalidDataException($"Traversal component is not allowed: {originalPath}");
        }

        if (component.EndsWith(' ') || component.EndsWith('.'))
        {
            throw new InvalidDataException($"Trailing spaces or dots are not allowed: {originalPath}");
        }

        if (component.AsSpan().IndexOfAny(InvalidComponentCharacters) >= 0 || component.Any(char.IsControl))
        {
            throw new InvalidDataException($"Invalid or ADS path component: {originalPath}");
        }

        string deviceName = component.Split('.', 2)[0];
        if (ReservedDeviceNames.Contains(deviceName))
        {
            throw new InvalidDataException($"Reserved device name is not allowed: {originalPath}");
        }
    }
}
