// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.TryRun.Core;

public static class WorkspacePath
{
    public const int MaximumEntries = 1000;
    public const int MaximumDepth = 16;
    public const long MaximumFileBytes = 32 * 1024 * 1024;
    public const long MaximumTotalBytes = 100 * 1024 * 1024;

    public static string ValidateRelative(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (path.Length > 240 || Path.IsPathRooted(path))
        {
            throw new ArgumentException("Workspace paths must be relative and at most 240 characters long.");
        }

        var parts = path.Replace('/', '\\').Split('\\');
        if (parts.Length > MaximumDepth)
        {
            throw new ArgumentException("Workspace folders may be at most 16 levels deep.");
        }

        foreach (var part in parts)
        {
            if (string.IsNullOrWhiteSpace(part) || part is "." or ".." || part.EndsWith(' ') || part.EndsWith('.') || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new ArgumentException("Workspace paths cannot contain empty names, traversal, streams, or invalid Windows names.");
            }

            var stem = part.Split('.')[0].TrimEnd(' ').ToUpperInvariant();
            if (stem is "CON" or "PRN" or "AUX" or "NUL" or "CONIN$" or "CONOUT$" ||
                (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) && "123456789¹²³".Contains(stem[3])))
            {
                throw new ArgumentException("Workspace paths cannot use Windows device names.");
            }
        }

        return string.Join('\\', parts);
    }

    internal static string LocalPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length < 3 || !char.IsAsciiLetter(path[0]) || path[1] != ':' || path[2] != '\\' || path[3..].Contains(':'))
        {
            throw new ArgumentException("Choose an absolute path on a local drive.");
        }

        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }

    internal static bool IsWithin(string path, string directory)
    {
        return string.Equals(path, directory, StringComparison.OrdinalIgnoreCase) || path.StartsWith(Path.TrimEndingDirectorySeparator(directory) + "\\", StringComparison.OrdinalIgnoreCase);
    }
}
