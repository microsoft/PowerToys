// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.CmdPal.Ext.Apps.Programs;

namespace Microsoft.CmdPal.Ext.Apps.Catalog.Sources;

internal sealed class PathEnvironmentAppSource : IWin32ProgramSource
{
    private readonly AllAppsSettings _settings;

    public PathEnvironmentAppSource(AllAppsSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public string Id => "path";

    public int Priority => 40;

    public bool IsEnabled => _settings.EnablePathEnvironmentVariableSource;

    public bool IncludeNonApps => true;

    public bool AsRunCommand => true;

    public string CacheKey => $"{IsEnabled}|{Environment.GetEnvironmentVariable("PATH")}|{string.Join(';', _settings.RunCommandSuffixes)}";

    public string ConfigurationKey => $"{IsEnabled}|{string.Join(';', _settings.RunCommandSuffixes)}";

    public IReadOnlyList<string> WatchPaths => [];

    public IEnumerable<string> GetPaths() => Win32Program.EnumeratePathEnvironmentPrograms(_settings.RunCommandSuffixes);

    public bool IsRelevantPath(string path) => false;
}
