// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.CmdPal.Ext.Apps.Programs;

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

    public RegistryAppSource(AllAppsSettings settings)
        : this(settings, Win32Program.EnumerateRegistryPrograms)
    {
    }

    internal RegistryAppSource(
        AllAppsSettings settings,
        Func<IList<string>, IEnumerable<(string CommandName, string TargetPath)>> enumeratePrograms)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _enumeratePrograms = enumeratePrograms ?? throw new ArgumentNullException(nameof(enumeratePrograms));
    }

    public IEnumerable<string> GetPaths()
    {
        foreach (var candidate in GetCandidates())
        {
            yield return candidate.Path;
        }
    }

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

    public bool IsRelevantPath(string path)
    {
        return false;
    }
}
