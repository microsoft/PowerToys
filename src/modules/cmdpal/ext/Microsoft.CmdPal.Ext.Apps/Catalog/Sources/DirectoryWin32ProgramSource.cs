// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CmdPal.Ext.Apps.Utils;

namespace Microsoft.CmdPal.Ext.Apps.Catalog.Sources;

/// <summary>
/// Provides common candidate enumeration, cache stamping, and watch roots for directory-backed Win32 origins.
/// </summary>
internal abstract class DirectoryWin32ProgramSource : IWin32ProgramSource
{
    private readonly IReadOnlyList<string> _directories;
    private readonly List<string> _suffixes;

    public abstract string Id { get; }

    public abstract int Priority { get; }

    public abstract bool IsEnabled { get; }

    public abstract Win32ProgramSourceProfile Profile { get; }

    public virtual int MaximumDepth
        => (Profile & Win32ProgramSourceProfile.RecurseSubdirectories) != 0 ? int.MaxValue : 0;

    public string CacheKey
    {
        get
        {
            List<string> stamps = [];
            foreach (var path in WatchPaths)
            {
                stamps.Add(DirectoryStamp(path));
            }

            return $"{IsEnabled}|{Profile}|{MaximumDepth}|{string.Join(';', _suffixes)}|{string.Join(';', stamps)}{ExclusionKey}";
        }
    }

    public string ConfigurationKey
        => $"{IsEnabled}|{Profile}|{MaximumDepth}|{string.Join(';', _suffixes)}|{string.Join(';', _directories)}{ExclusionKey}";

    public IReadOnlyList<string> WatchPaths
    {
        get
        {
            List<string> paths = [];
            var uniquePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var directory in _directories)
            {
                if (string.IsNullOrWhiteSpace(directory))
                {
                    continue;
                }

                var normalizedPath = PathHelpers.NormalizePath(directory);
                if (uniquePaths.Add(normalizedPath))
                {
                    paths.Add(normalizedPath);
                }
            }

            return paths;
        }
    }

    public IEnumerable<string> GetPaths()
    {
        foreach (var directory in WatchPaths)
        {
            foreach (var path in Win32Program.EnumerateProgramPaths(directory, _suffixes, MaximumDepth))
            {
                if (!IsExcludedPath(path))
                {
                    yield return path;
                }
            }
        }
    }

    public IEnumerable<string> GetPathsForChange(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            yield break;
        }

        var normalizedPath = PathHelpers.NormalizePath(path);
        if (IsExcludedPath(normalizedPath))
        {
            yield break;
        }

        if (File.Exists(normalizedPath))
        {
            if (HasSupportedSuffix(normalizedPath) && IsCandidateFileInSource(normalizedPath))
            {
                yield return normalizedPath;
            }

            yield break;
        }

        if (!Directory.Exists(normalizedPath))
        {
            yield break;
        }

        var emittedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var configuredDirectory in _directories)
        {
            var sourceRoot = PathHelpers.NormalizePath(configuredDirectory);
            if (!Directory.Exists(sourceRoot))
            {
                continue;
            }

            string enumerationRoot;
            int maximumDepth;
            if (TryGetDirectoryDepth(sourceRoot, normalizedPath, out var dirtyDirectoryDepth))
            {
                if (dirtyDirectoryDepth > MaximumDepth)
                {
                    continue;
                }

                enumerationRoot = normalizedPath;
                maximumDepth = MaximumDepth == int.MaxValue
                    ? int.MaxValue
                    : MaximumDepth - dirtyDirectoryDepth;
            }
            else if (TryGetDirectoryDepth(normalizedPath, sourceRoot, out _))
            {
                enumerationRoot = sourceRoot;
                maximumDepth = MaximumDepth;
            }
            else
            {
                continue;
            }

            foreach (var candidatePath in Win32Program.EnumerateProgramPaths(enumerationRoot, _suffixes, maximumDepth))
            {
                if (!IsExcludedPath(candidatePath) && emittedPaths.Add(candidatePath))
                {
                    yield return candidatePath;
                }
            }
        }
    }

    public bool IsRelevantPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var extension = Path.GetExtension(path).TrimStart('.');
        var normalizedPath = PathHelpers.NormalizePath(path);
        if (IsExcludedPath(normalizedPath))
        {
            return false;
        }

        var isSupportedFile = HasSupportedSuffix(path);
        if (!isSupportedFile && File.Exists(normalizedPath))
        {
            return false;
        }

        var isKnownOrLikelyDirectory = string.IsNullOrEmpty(extension) || Directory.Exists(path);
        var candidateDirectory = isKnownOrLikelyDirectory ? normalizedPath : Path.GetDirectoryName(normalizedPath);
        if (string.IsNullOrEmpty(candidateDirectory))
        {
            return false;
        }

        foreach (var configuredDirectory in _directories)
        {
            if (!TryGetDirectoryDepth(PathHelpers.NormalizePath(configuredDirectory), candidateDirectory, out var depth))
            {
                continue;
            }

            var isRelevantDepth = isKnownOrLikelyDirectory || isSupportedFile
                ? depth <= MaximumDepth
                : depth < MaximumDepth;
            if (isRelevantDepth)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsCandidateFileInSource(string path)
    {
        var parentDirectory = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(parentDirectory))
        {
            return false;
        }

        foreach (var configuredDirectory in _directories)
        {
            if (TryGetDirectoryDepth(PathHelpers.NormalizePath(configuredDirectory), parentDirectory, out var depth)
                && depth <= MaximumDepth)
            {
                return true;
            }
        }

        return false;
    }

    private bool HasSupportedSuffix(string path)
    {
        var extension = Path.GetExtension(path).TrimStart('.');
        foreach (var suffix in _suffixes)
        {
            if (string.Equals(extension, suffix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetDirectoryDepth(string root, string candidate, out int depth)
    {
        try
        {
            var relativePath = Path.GetRelativePath(
                PathHelpers.NormalizePath(root),
                PathHelpers.NormalizePath(candidate));
            if (string.Equals(relativePath, ".", StringComparison.Ordinal))
            {
                depth = 0;
                return true;
            }

            if (Path.IsPathRooted(relativePath)
                || string.Equals(relativePath, "..", StringComparison.Ordinal)
                || relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal))
            {
                depth = 0;
                return false;
            }

            depth = 1;
            foreach (var character in relativePath)
            {
                if (character == Path.DirectorySeparatorChar
                    || character == Path.AltDirectorySeparatorChar)
                {
                    depth++;
                }
            }

            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            depth = 0;
            return false;
        }
    }

    private static string DirectoryStamp(string path)
    {
        try
        {
            if (!Directory.Exists(path))
            {
                return $"{path}:unavailable";
            }

            return $"{path}:{Directory.GetLastWriteTimeUtc(path).Ticks}";
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException
            or PathTooLongException)
        {
            return $"{path}:unavailable";
        }
    }

    private readonly IReadOnlyList<string> _excludedDirectories;

    private string ExclusionKey => _excludedDirectories.Count == 0
        ? string.Empty
        : $"|exclude:{string.Join(';', _excludedDirectories)}";

    protected DirectoryWin32ProgramSource(
        IReadOnlyList<string> directories,
        IReadOnlyList<string> suffixes,
        IReadOnlyList<string>? excludedDirectories = null)
    {
        _directories = directories ?? throw new ArgumentNullException(nameof(directories));
        _excludedDirectories = excludedDirectories ?? [];
        _suffixes = new List<string>(suffixes ?? throw new ArgumentNullException(nameof(suffixes)));
    }

    private bool IsExcludedPath(string path)
    {
        foreach (var directory in _excludedDirectories)
        {
            if (!string.IsNullOrWhiteSpace(directory) && TryGetDirectoryDepth(directory, path, out _))
            {
                return true;
            }
        }

        return false;
    }
}
