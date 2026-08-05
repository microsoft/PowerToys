// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.CmdPal.Ext.Apps.Programs;

namespace Microsoft.CmdPal.Ext.Apps.Catalog.Sources;

internal sealed class RegistryAppSource : IWin32ProgramSource
{
    private readonly AllAppsSettings _settings;

    public RegistryAppSource(AllAppsSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public string Id => "registry";

    public int Priority => 30;

    public bool IsEnabled => _settings.EnableRegistrySource;

    public bool IncludeNonApps => true;

    public bool AsRunCommand => false;

    public string CacheKey => $"{IsEnabled}|{string.Join(';', _settings.ProgramSuffixes)}";

    public string ConfigurationKey => CacheKey;

    public IReadOnlyList<string> WatchPaths => [];

    public IEnumerable<string> GetPaths() => Win32Program.EnumerateRegistryPrograms(_settings.ProgramSuffixes);

    public bool IsRelevantPath(string path) => false;
}
