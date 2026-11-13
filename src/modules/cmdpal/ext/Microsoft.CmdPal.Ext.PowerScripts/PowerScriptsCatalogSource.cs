// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.PowerScripts.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerScripts.Client;

namespace Microsoft.CmdPal.Ext.PowerScripts;

/// <summary>Shares script discovery and command lifetimes between the script list and top-level results.</summary>
internal sealed partial class PowerScriptsCatalogSource : IDisposable
{
    private static readonly TimeSpan CatalogLifetime = TimeSpan.FromSeconds(30);
    private readonly object _gate = new();
    private readonly IPowerScriptsService _service;
    private readonly Action<IPage> _navigate;
    private readonly TimeProvider _timeProvider;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly CancellationToken _lifetimeToken;
    private readonly Dictionary<string, PowerScriptInfo> _currentScripts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ScriptEntry> _entries = new(StringComparer.Ordinal);
    private readonly AnonymousCommand _refresh;
    private readonly OpenPowerScriptsSettingsCommand _settings = new();
    private PowerScriptsCatalog? _catalog;
    private string? _error;
    private bool _discovering;
    private bool _refreshRequested;
    private bool _disposed;
    private Task<DiscoveryResult>? _discovery;
    private DateTimeOffset _lastDiscovery;

    internal PowerScriptsCatalogSource(IPowerScriptsService service, Action<IPage> navigate, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(navigate);
        _service = service;
        _navigate = navigate;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _lifetimeToken = _lifetime.Token;
        _refresh = new AnonymousCommand(Refresh)
        {
            Id = "com.microsoft.cmdpal.builtin.powerscripts.refresh",
            Name = Resources.Refresh,
            Icon = new IconInfo("\uE72C"),
            Result = CommandResult.KeepOpen(),
        };
    }

    internal event EventHandler? Changed;

    internal event EventHandler? RefreshCompleted;

    internal PowerScriptsCatalog? Catalog
    {
        get
        {
            lock (_gate)
            {
                return _catalog;
            }
        }
    }

    internal string? Error
    {
        get
        {
            lock (_gate)
            {
                return _error;
            }
        }
    }

    internal bool IsDiscovering
    {
        get
        {
            lock (_gate)
            {
                return _discovering;
            }
        }
    }

    internal void EnsureDiscovery()
    {
        _ = StartDiscovery(force: false);
    }

    internal void Refresh()
    {
        _ = StartDiscovery(force: true);
    }

    internal async Task<PowerScriptsCatalog> GetCatalogAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var discovery = StartDiscovery(force: false)
            ?? throw new ObjectDisposedException(nameof(PowerScriptsCatalogSource));

        // The shared discovery belongs to the provider. A replaced query only stops waiting for it.
        var result = await discovery.WaitAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }

        result.Error?.Throw();
        return result.Catalog!;
    }

    internal IListItem[] GetItems(PowerScriptsCatalog catalog, string query, int maximumItems = int.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(query);
        lock (_gate)
        {
            if (_disposed || !catalog.IsEnabled || _catalog is not { IsEnabled: true } || _error is not null || maximumItems <= 0)
            {
                return [];
            }

            var scripts = catalog.Scripts
                .Where(script => _currentScripts.ContainsKey(script.Id))
                .Where(script => string.IsNullOrWhiteSpace(query) ||
                    script.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                    script.Description.Contains(query, StringComparison.CurrentCultureIgnoreCase))
                .OrderBy(script => script.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(script => script.Id, StringComparer.Ordinal)
                .Take(maximumItems);
            var items = new List<IListItem>();
            foreach (var snapshot in scripts)
            {
                // A query or continuation may retain an older catalog. Its rows keep their query
                // text, but commands and forms must use the current parameter contract.
                var script = _currentScripts[snapshot.Id];
                if (!_entries.TryGetValue(script.Id, out var entry))
                {
                    var session = new PowerScriptRunSession(_service, script);
                    entry = new ScriptEntry(session, new PowerScriptPage(_service, script, session));
                    _entries.Add(script.Id, entry);
                }

                ICommand command = entry.Page;
                if (script.Parameters.Count == 0)
                {
                    entry.RunCommand ??= new RunPowerScriptCommand(script, entry.Session, entry.Page, _navigate);
                    command = entry.RunCommand;
                }

                items.Add(new ListItem(command)
                {
                    Title = snapshot.Name,
                    Subtitle = snapshot.Description,
                    Icon = entry.Page.Icon,
                    MoreCommands =
                    [
                        new CommandContextItem(_refresh) { Title = Resources.Refresh },
                        new CommandContextItem(_settings)
                        {
                            Title = Resources.OpenSettings,
                            Subtitle = Resources.OpenSettingsDescription,
                        },
                    ],
                });
            }

            return [.. items];
        }
    }

    public void Dispose()
    {
        ScriptEntry[] entries;
        Task<DiscoveryResult>? discovery;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            entries = [.. _entries.Values];
            _entries.Clear();
            _currentScripts.Clear();
            discovery = _discovery;
        }

        // Cancellation can synchronously complete discovery or a run. Do not hold the catalog lock.
        _lifetime.Cancel();
        foreach (var entry in entries)
        {
            entry.Page.Dispose();
            entry.Session.Dispose();
        }

        (_service as IDisposable)?.Dispose();

        if (discovery is null || discovery.IsCompleted)
        {
            _lifetime.Dispose();
        }
        else
        {
            _ = DisposeLifetimeAsync(discovery);
        }
    }

    private Task<DiscoveryResult>? StartDiscovery(bool force)
    {
        Task<DiscoveryResult> discovery;
        lock (_gate)
        {
            if (_disposed)
            {
                return null;
            }

            if (_discovering)
            {
                _refreshRequested |= force;
                return _discovery;
            }

            if (!force && _discovery is not null && _timeProvider.GetUtcNow() - _lastDiscovery < CatalogLifetime)
            {
                return _discovery;
            }

            _discovering = true;
            _refreshRequested = force;
            _error = null;
            discovery = Task.Run(DiscoverAsync);
            _discovery = discovery;
        }

        Notify(Changed);
        return discovery;
    }

    private async Task<DiscoveryResult> DiscoverAsync()
    {
        PowerScriptsCatalog? catalog = null;
        ExceptionDispatchInfo? error = null;
        try
        {
            catalog = await _service.DiscoverAsync(_lifetimeToken).ConfigureAwait(false);
            lock (_gate)
            {
                if (!_disposed)
                {
                    _catalog = catalog;
                    _currentScripts.Clear();
                    foreach (var script in catalog.Scripts)
                    {
                        _currentScripts[script.Id] = script;
                        if (_entries.TryGetValue(script.Id, out var entry))
                        {
                            entry.Page.UpdateScript(script);
                            entry.RunCommand?.UpdateScript(script);
                        }
                    }

                    // Removed scripts disappear from the catalog, but a visible result or open page
                    // may still hold their command. Keep those objects alive until the provider closes.
                }
            }
        }
        catch (OperationCanceledException) when (_lifetimeToken.IsCancellationRequested)
        {
            // Provider disposal is not a user-visible discovery failure.
        }
        catch (OperationCanceledException ex)
        {
            error = ExceptionDispatchInfo.Capture(new TimeoutException(Resources.DiscoveryTimedOut, ex));
        }
        catch (Exception ex)
        {
            error = ExceptionDispatchInfo.Capture(ex);
        }

        bool notify;
        bool refreshCompleted;
        lock (_gate)
        {
            _discovering = false;
            notify = !_disposed;
            refreshCompleted = notify && _refreshRequested;
            _refreshRequested = false;
            if (notify)
            {
                _error = error?.SourceException.Message;
                _lastDiscovery = _timeProvider.GetUtcNow();
            }
        }

        if (notify)
        {
            Notify(Changed);
            if (refreshCompleted)
            {
                Notify(RefreshCompleted);
            }
        }

        return new DiscoveryResult(catalog, error);
    }

    private async Task DisposeLifetimeAsync(Task discovery)
    {
        await discovery.ConfigureAwait(false);
        _lifetime.Dispose();
    }

    private void Notify(EventHandler? handler)
    {
        try
        {
            handler?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            // A closing view must not fail discovery shared with another view.
        }
    }

    private sealed class ScriptEntry(PowerScriptRunSession session, PowerScriptPage page)
    {
        internal PowerScriptRunSession Session { get; } = session;

        internal PowerScriptPage Page { get; } = page;

        internal RunPowerScriptCommand? RunCommand { get; set; }
    }

    private sealed record DiscoveryResult(PowerScriptsCatalog? Catalog, ExceptionDispatchInfo? Error);
}
