// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.CmdPal.Ext.Apps.Utils;

namespace Microsoft.CmdPal.Ext.Apps.Catalog.Sources;

internal sealed class PathEnvironmentAppSource : IWin32ProgramSource
{
    private readonly AllAppsSettings _settings;

    public string Id => "path";

    public int Priority => 40;

    public bool IsEnabled => _settings.EnablePathEnvironmentVariableSource;

    public Win32ProgramSourceProfile Profile
        => Win32ProgramSourceProfile.IncludeRawExecutables | Win32ProgramSourceProfile.LoadAsRunCommand;

    public string CacheKey => $"{IsEnabled}|{Profile}|{Environment.GetEnvironmentVariable("PATH")}|{string.Join(';', _settings.RunCommandSuffixes)}";

    public string ConfigurationKey => $"{IsEnabled}|{Profile}|{string.Join(';', _settings.RunCommandSuffixes)}";

    public IReadOnlyList<string> WatchPaths => [];

    /// <summary>Initializes a new instance of the <see cref="PathEnvironmentAppSource"/> class. Creates PATH discovery using the configured Run command suffixes.</summary>
    public PathEnvironmentAppSource(AllAppsSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _settings = settings;
    }

    /// <inheritdoc />
    public IEnumerable<string> GetPaths()
    {
        return EnumeratePaths(_settings.RunCommandSuffixes);
    }

    /// <inheritdoc />
    public IEnumerable<Win32ProgramCandidate> GetCandidates(Action<string, Exception>? onError = null, CancellationToken cancellationToken = default)
    {
        foreach (var path in EnumeratePaths(_settings.RunCommandSuffixes, onError, cancellationToken))
        {
            yield return new Win32ProgramCandidate(path, []);
        }
    }

    /// <inheritdoc />
    public bool IsRelevantPath(string path)
    {
        return false;
    }

    private static IEnumerable<string> EnumeratePaths(
        IList<string> suffixes,
        Action<string, Exception>? onError = null,
        CancellationToken cancellationToken = default)
    {
        // To get all the locations stored in the PATH env variable
        var pathEnvVariable = Environment.GetEnvironmentVariable("PATH");
        var searchPaths = pathEnvVariable?.Split(System.IO.Path.PathSeparator);
        var toFilterAllPaths = new List<string>();
        if (searchPaths is not null)
        {
            foreach (var path in searchPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var directory = Environment.ExpandEnvironmentVariables(path).Trim('"', ' ');
                if (directory.Length > 0)
                {
                    var paths = Win32FileEnumerator.EnumerateFiles(directory, suffixes, maximumDepth: 0, onError: onError, cancellationToken: cancellationToken);
                    toFilterAllPaths.AddRange(paths);
                }
            }
        }

        return toFilterAllPaths;
    }
}
