// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading;
using ManagedCommon;
using Microsoft.Win32;

namespace Microsoft.CmdPal.Ext.Apps.Catalog.Sources;

internal sealed class RegistryAppSource : IWin32ProgramSource
{
    private readonly AllAppsSettings _settings;
    private readonly Func<IList<string>, IEnumerable<(string CommandName, string TargetPath)>> _enumeratePrograms;

    public string Id => "registry";

    public int Priority => 30;

    public bool IsEnabled => _settings.EnableRegistrySource;

    public Win32ProgramSourceProfile Profile => Win32ProgramSourceProfile.IncludeRawExecutables;

    public string CacheKey => $"{IsEnabled}|{Profile}|{string.Join(';', _settings.ProgramSuffixes)}";

    public string ConfigurationKey => CacheKey;

    public IReadOnlyList<string> WatchPaths => [];

    /// <summary>Initializes a new instance of the <see cref="RegistryAppSource"/> class. Creates App Paths discovery that preserves registry command names as search terms.</summary>
    public RegistryAppSource(AllAppsSettings settings)
        : this(settings, EnumerateRegistryPrograms)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="RegistryAppSource"/> class. Creates App Paths discovery with an injected registry command and target enumerator.</summary>
    internal RegistryAppSource(
        AllAppsSettings settings,
        Func<IList<string>, IEnumerable<(string CommandName, string TargetPath)>> enumeratePrograms)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(enumeratePrograms);

        _settings = settings;
        _enumeratePrograms = enumeratePrograms;
    }

    /// <inheritdoc />
    public IEnumerable<string> GetPaths()
    {
        foreach (var candidate in GetCandidates())
        {
            yield return candidate.Path;
        }
    }

    /// <inheritdoc />
    public IEnumerable<Win32ProgramCandidate> GetCandidates(Action<string, Exception>? onError = null, CancellationToken cancellationToken = default)
    {
        var termsByPath = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var program in _enumeratePrograms(_settings.ProgramSuffixes))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!termsByPath.TryGetValue(program.TargetPath, out var terms))
            {
                terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                termsByPath.Add(program.TargetPath, terms);
            }

            if (!string.IsNullOrWhiteSpace(program.CommandName))
            {
                terms.Add(program.CommandName);
                terms.Add(Path.GetFileNameWithoutExtension(program.CommandName));
            }
        }

        foreach (var pair in termsByPath)
        {
            yield return new Win32ProgramCandidate(pair.Key, pair.Value.ToArray());
        }
    }

    /// <inheritdoc />
    public bool IsRelevantPath(string path)
    {
        return false;
    }

    private static List<(string CommandName, string TargetPath)> EnumerateRegistryPrograms(IList<string> suffixes)
    {
        // https://msdn.microsoft.com/library/windows/desktop/ee872121
        const string appPaths = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths";
        var programs = new List<(string CommandName, string TargetPath)>();
        using (var root = Registry.LocalMachine.OpenSubKey(appPaths))
        {
            if (root is not null)
            {
                programs.AddRange(GetProgramsFromRegistry(root));
            }
        }

        using (var root = Registry.CurrentUser.OpenSubKey(appPaths))
        {
            if (root is not null)
            {
                programs.AddRange(GetProgramsFromRegistry(root));
            }
        }

        var returnedPrograms = new List<(string CommandName, string TargetPath)>();
        foreach (var program in programs)
        {
            var matchesSuffix = false;
            foreach (var suffix in suffixes)
            {
                if (program.TargetPath.EndsWith(suffix, StringComparison.InvariantCultureIgnoreCase))
                {
                    matchesSuffix = true;
                    break;
                }
            }

            if (matchesSuffix)
            {
                returnedPrograms.Add((program.CommandName, Environment.ExpandEnvironmentVariables(program.TargetPath)));
            }
        }

        return returnedPrograms;
    }

    private static IEnumerable<(string CommandName, string TargetPath)> GetProgramsFromRegistry(RegistryKey root)
    {
        var result = new List<(string CommandName, string TargetPath)>();

        // Get all subkey names
        var subKeyNames = root.GetSubKeyNames();

        // Process each subkey to extract the path
        foreach (var subkeyName in subKeyNames)
        {
            var path = GetPathFromRegistrySubkey(root, subkeyName);
            if (!string.IsNullOrEmpty(path))
            {
                result.Add((subkeyName, path));
            }
        }

        return result;
    }

    private static string GetPathFromRegistrySubkey(RegistryKey root, string subkey)
    {
        var path = string.Empty;
        try
        {
            using (var key = root.OpenSubKey(subkey))
            {
                if (key is null)
                {
                    return string.Empty;
                }

                path = key.GetValue(string.Empty) as string ?? string.Empty;
            }

            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            return path.Trim('"', ' ');
        }
        catch (Exception e) when (e is SecurityException || e is UnauthorizedAccessException)
        {
            Logger.LogError(e.Message);
            return string.Empty;
        }
    }
}
