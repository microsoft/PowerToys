// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.CmdPal.Ext.Apps.Programs;

namespace Microsoft.CmdPal.Ext.Apps.Catalog.Sources;

/// <summary>
/// Provides common candidate enumeration, cache stamping, and watch roots for directory-backed Win32 origins.
/// </summary>
internal abstract class DirectoryWin32ProgramSource : IWin32ProgramSource
{
    private readonly IReadOnlyList<string> _directories;
    private readonly List<string> _suffixes;

    protected DirectoryWin32ProgramSource(IReadOnlyList<string> directories, IReadOnlyList<string> suffixes)
    {
        _directories = directories ?? throw new ArgumentNullException(nameof(directories));
        _suffixes = new List<string>(suffixes ?? throw new ArgumentNullException(nameof(suffixes)));
    }

    public abstract string Id { get; }

    public abstract int Priority { get; }

    public abstract bool IsEnabled { get; }

    public abstract bool IncludeNonApps { get; }

    public virtual bool AsRunCommand => false;

    public string CacheKey
    {
        get
        {
            List<string> stamps = [];
            foreach (var path in WatchPaths)
            {
                stamps.Add(DirectoryStamp(path));
            }

            return $"{IsEnabled}|{IncludeNonApps}|{string.Join(';', _suffixes)}|{string.Join(';', stamps)}";
        }
    }

    public string ConfigurationKey
        => $"{IsEnabled}|{IncludeNonApps}|{string.Join(';', _suffixes)}|{string.Join(';', _directories)}";

    public IReadOnlyList<string> WatchPaths
    {
        get
        {
            List<string> paths = [];
            var uniquePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var directory in _directories)
            {
                if (Directory.Exists(directory) && uniquePaths.Add(directory))
                {
                    paths.Add(directory);
                }
            }

            return paths;
        }
    }

    public IEnumerable<string> GetPaths()
    {
        foreach (var directory in WatchPaths)
        {
            foreach (var path in Win32Program.EnumerateProgramPaths(directory, _suffixes))
            {
                yield return path;
            }
        }
    }

    public bool IsRelevantPath(string path)
    {
        var extension = Path.GetExtension(path).TrimStart('.');
        if (string.IsNullOrEmpty(extension))
        {
            return true;
        }

        foreach (var suffix in _suffixes)
        {
            if (string.Equals(extension, suffix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string DirectoryStamp(string path)
    {
        try
        {
            return $"{path}:{Directory.GetLastWriteTimeUtc(path).Ticks}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return $"{path}:unavailable";
        }
    }
}
