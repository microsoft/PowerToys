// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.CmdPal.Ext.Apps.Catalog.Sources;

/// <summary>
/// Projects All Apps settings into stable, independently refreshable catalog source instances.
/// </summary>
internal sealed partial class SettingsAppSourceProvider : IAppSourceProvider
{
    private readonly Lock _stateLock = new();
    private readonly AllAppsSettings _settings;
    private readonly IAppSource _packagedSource;
    private readonly ILogger<Win32AppSource> _win32Logger;
    private Dictionary<string, Win32AppSource> _win32Sources = new(StringComparer.Ordinal);
    private IReadOnlyList<IAppSource> _sources = [];
    private bool _disposed;

    public SettingsAppSourceProvider(
        AllAppsSettings settings,
        IAppSource packagedSource,
        ILogger<Win32AppSource>? win32Logger = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _packagedSource = packagedSource ?? throw new ArgumentNullException(nameof(packagedSource));
        _win32Logger = win32Logger ?? NullLogger<Win32AppSource>.Instance;
        RebuildSources(raiseChanged: false);
        _settings.Settings.SettingsChanged += OnSettingsChanged;
    }

    public event EventHandler? Changed;

    public IReadOnlyList<IAppSource> GetSources()
    {
        lock (_stateLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _sources;
        }
    }

    private void OnSettingsChanged(object sender, Settings args) => RebuildSources(raiseChanged: true);

    private void RebuildSources(bool raiseChanged)
    {
        Dictionary<string, Win32AppSource> retainedSources;
        List<IAppSource> sources = [_packagedSource];
        var changed = false;

        lock (_stateLock)
        {
            if (_disposed)
            {
                return;
            }

            var configuredSources = Win32AppSource.CreateProgramSources(_settings);
            retainedSources = new Dictionary<string, Win32AppSource>(StringComparer.Ordinal);
            foreach (var configuredSource in configuredSources)
            {
                var sourceId = "win32:" + configuredSource.Id;
                if (retainedSources.ContainsKey(sourceId))
                {
                    continue;
                }

                Win32AppSource source;
                if (_win32Sources.TryGetValue(sourceId, out var existing)
                    && string.Equals(existing.ConfigurationKey, configuredSource.ConfigurationKey, StringComparison.Ordinal))
                {
                    source = existing;
                }
                else
                {
                    source = new Win32AppSource(configuredSource, _win32Logger);
                    changed = true;
                }

                retainedSources.Add(sourceId, source);
                sources.Add(source);
            }

            if (_win32Sources.Count != retainedSources.Count)
            {
                changed = true;
            }

            _win32Sources = retainedSources;
            _sources = sources.AsReadOnly();
        }

        if (raiseChanged && changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose()
    {
        lock (_stateLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _settings.Settings.SettingsChanged -= OnSettingsChanged;
            Changed = null;
        }
    }
}
