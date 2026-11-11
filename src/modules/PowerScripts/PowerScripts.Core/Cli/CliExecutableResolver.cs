// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerScripts.Core.Cli;

/// <summary>Resolves catalogue executables without consulting PATH or user environment overrides.</summary>
internal static class CliExecutableResolver
{
    internal static bool IsValidRelativeExecutable(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath) ||
            relativePath.Contains(':') || relativePath.Contains('\0') ||
            !string.Equals(Path.GetExtension(relativePath), ".exe", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return relativePath.Split(['\\', '/']).All(segment =>
            !string.IsNullOrWhiteSpace(segment) && segment is not "." and not ".." &&
            segment == segment.TrimEnd(' ', '.') && segment.IndexOfAny(Path.GetInvalidFileNameChars()) < 0);
    }

    internal static bool TryResolve(string installationRoot, string relativePath, out string? executablePath, out string reason)
    {
        executablePath = null;
        reason = string.Empty;
        if (!Path.IsPathFullyQualified(installationRoot) || !IsValidRelativeExecutable(relativePath))
        {
            reason = "The command must use a relative .exe path inside an absolute PowerToys installation directory.";
            return false;
        }

        try
        {
            var root = Path.GetFullPath(installationRoot);
            if (!Directory.Exists(root))
            {
                reason = "The PowerToys installation directory is unavailable.";
                return false;
            }

            var path = Path.GetFullPath(Path.Combine(root, relativePath.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar)));
            var relative = Path.GetRelativePath(root, path);
            if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                reason = "The executable path escapes the PowerToys installation directory.";
                return false;
            }

            // Check the target and every parent, including ancestors of the installation root.
            // A lexical containment check alone would still follow a junction or symbolic link.
            var current = path;
            while (!string.IsNullOrEmpty(current))
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                {
                    reason = "Executable paths through symbolic links or junctions are not supported.";
                    return false;
                }

                current = Path.GetDirectoryName(current);
            }

            if (!File.Exists(path))
            {
                reason = $"The command executable '{relativePath}' is not installed.";
                return false;
            }

            executablePath = path;
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            reason = $"The command executable '{relativePath}' is unavailable: {ex.Message}";
            return false;
        }
    }
}
