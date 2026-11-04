// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace FileExplorerScripts.Core;

internal static class LocalPaths
{
    internal const int MaxCount = 1024;
    internal const int MaxPathLength = 259;

    internal static string Validate(string path, bool directory)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > MaxPathLength ||
            path.Length < 3 || !char.IsAsciiLetter(path[0]) || path[1] != ':' || path[2] != '\\' ||
            path.Contains('/') || path.AsSpan(2).Contains(':') || (!directory && path.EndsWith('\\')))
        {
            throw new ArgumentException("Use an absolute local drive path of at most 259 characters, not a UNC, device, or stream path.", nameof(path));
        }

        var full = Path.TrimEndingDirectorySeparator(path);
        foreach (var component in full[3..].Split('\\', StringSplitOptions.None))
        {
            if (full.Length > 3 && !IsSafeName(component))
            {
                throw new ArgumentException("The path contains an unsafe filename or traversal component.", nameof(path));
            }
        }

        if (new DriveInfo(full[..3]).DriveType is not (DriveType.Fixed or DriveType.Removable or DriveType.Ram))
        {
            throw new ArgumentException("Only local fixed, removable, or RAM drives are supported.", nameof(path));
        }

        var attributes = File.GetAttributes(full);
        if (((attributes & FileAttributes.Directory) != 0) != directory ||
            (attributes & (FileAttributes.ReparsePoint | FileAttributes.Device)) != 0)
        {
            throw new ArgumentException(directory ? "Destination must be a real local folder, not a reparse point." : "Selection/output must be a regular file, not a folder or reparse point.", nameof(path));
        }

        for (var parent = directory ? full : Path.GetDirectoryName(full); parent is not null; parent = Path.GetDirectoryName(parent))
        {
            if ((File.GetAttributes(parent) & FileAttributes.ReparsePoint) != 0)
            {
                throw new ArgumentException("Paths through reparse-point folders are unsupported.", nameof(path));
            }
        }

        return full;
    }

    internal static bool IsSafeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 255 || name is "." or ".." ||
            name.EndsWith('.') || name.EndsWith(' ') || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return false;
        }

        var stem = name.Split('.')[0].TrimEnd(' ');
        return !stem.Equals("CON", StringComparison.OrdinalIgnoreCase) &&
            !stem.Equals("PRN", StringComparison.OrdinalIgnoreCase) &&
            !stem.Equals("AUX", StringComparison.OrdinalIgnoreCase) &&
            !stem.Equals("NUL", StringComparison.OrdinalIgnoreCase) &&
            !stem.Equals("CONIN$", StringComparison.OrdinalIgnoreCase) &&
            !stem.Equals("CONOUT$", StringComparison.OrdinalIgnoreCase) &&
            !(stem.Length == 4 &&
                (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) &&
                (stem[3] is >= '0' and <= '9' or '¹' or '²' or '³'));
    }
}
