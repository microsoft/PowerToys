// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.PowerScripts.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerScripts.Client;

namespace Microsoft.CmdPal.Ext.PowerScripts;

internal sealed partial class PowerScriptsListPage : DynamicListPage, IDisposable
{
    private static readonly TimeSpan CatalogLifetime = TimeSpan.FromSeconds(30);
    private readonly object _gate = new();
    private readonly IPowerScriptsService _service;
    private readonly Action<IPage> _navigate;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Dictionary<string, PowerScriptRunSession> _sessions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PowerScriptPage> _pages = new(StringComparer.Ordinal);
    private readonly Dictionary<string, RunPowerScriptCommand> _runCommands = new(StringComparer.Ordinal);
    private readonly AnonymousCommand _refresh;
    private readonly OpenPowerScriptsSettingsCommand _settings = new();
    private PowerScriptsCatalog? _catalog;
    private string? _error;
    private bool _started;
    private bool _discovering;
    private bool _disposed;
    private Task? _discovery;
    private DateTimeOffset _lastDiscovery;

    internal PowerScriptsListPage(IPowerScriptsService service, Action<IPage> navigate)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(navigate);
        _service = service;
        _navigate = navigate;
        Id = "com.microsoft.cmdpal.builtin.powerscripts.list";
        Name = Resources.Open;
        Title = Resources.PowerScripts;
        Icon = new IconInfo("\uE756");
        PlaceholderText = Resources.SearchPlaceholder;
        _refresh = new AnonymousCommand(StartDiscovery)
        {
            Id = "com.microsoft.cmdpal.builtin.powerscripts.refresh",
            Name = Resources.Refresh,
            Icon = new IconInfo("\uE72C"),
            Result = CommandResult.KeepOpen(),
        };
    }

    public override void UpdateSearchText(string oldSearch, string newSearch) => RaiseItemsChanged();

    public override IListItem[] GetItems()
    {
        bool startDiscovery;
        lock (_gate)
        {
            startDiscovery = !_disposed && !_discovering && (!_started || DateTimeOffset.UtcNow - _lastDiscovery >= CatalogLifetime);
        }

        if (startDiscovery)
        {
            StartDiscovery();
        }

        lock (_gate)
        {
            if (_disposed)
            {
                return [];
            }

            if (_discovering)
            {
                EmptyContent = new CommandItem(new NoOpCommand())
                {
                    Title = Resources.Loading,
                    Subtitle = Resources.LoadingDescription,
                    Icon = Icon,
                };
                return [];
            }

            if (_error is not null)
            {
                EmptyContent = new CommandItem(_refresh)
                {
                    Title = Resources.DiscoveryFailed,
                    Subtitle = _error,
                    Icon = Icon,
                    MoreCommands = [SettingsContextItem()],
                };
                return [];
            }

            if (_catalog is null || !_catalog.IsEnabled)
            {
                EmptyContent = new CommandItem(_settings)
                {
                    Title = _catalog?.IsPolicyControlled == true ? Resources.DisabledByPolicy : Resources.Disabled,
                    Subtitle = DisabledHint(),
                    Icon = Icon,
                    MoreCommands = [new CommandContextItem(_refresh) { Title = Resources.Refresh }],
                };
                return [];
            }

            var scripts = _catalog.Scripts
                .Where(script => string.IsNullOrWhiteSpace(SearchText) ||
                    script.Name.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase) ||
                    script.Description.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase))
                .OrderBy(script => script.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(script => script.Id, StringComparer.Ordinal);
            var items = new List<IListItem>();
            foreach (var script in scripts)
            {
                if (!_pages.TryGetValue(script.Id, out var page))
                {
                    var session = new PowerScriptRunSession(_service, script);
                    page = new PowerScriptPage(_service, script, session);
                    _sessions.Add(script.Id, session);
                    _pages.Add(script.Id, page);
                }

                ICommand command = page;
                if (script.Parameters.Count == 0)
                {
                    if (!_runCommands.TryGetValue(script.Id, out var runCommand))
                    {
                        runCommand = new RunPowerScriptCommand(script, _sessions[script.Id], page, _navigate);
                        _runCommands.Add(script.Id, runCommand);
                    }

                    command = runCommand;
                }

                items.Add(new ListItem(command)
                {
                    Title = script.Name,
                    Subtitle = script.Description,
                    Icon = page.Icon,
                    MoreCommands = [new CommandContextItem(_refresh) { Title = Resources.Refresh }, SettingsContextItem()],
                });
            }

            if (items.Count == 0)
            {
                var empty = _catalog.Scripts.Count == 0;
                EmptyContent = new CommandItem(empty ? _settings : (ICommand)_refresh)
                {
                    Title = empty ? Resources.NoScripts : Resources.NoMatches,
                    Subtitle = empty ? Resources.NoScriptsDescription : Resources.NoMatchesDescription,
                    Icon = Icon,
                    MoreCommands = empty
                        ? [new CommandContextItem(_refresh) { Title = Resources.Refresh }]
                        : [SettingsContextItem()],
                };
                return [];
            }

            EmptyContent = null;
            return [.. items];
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _lifetime.Cancel();
            foreach (var page in _pages.Values)
            {
                page.Dispose();
            }

            foreach (var session in _sessions.Values)
            {
                session.Dispose();
            }

            // Dispose only after the discovery task no longer needs its token.
            if (_discovery is null || _discovery.IsCompleted)
            {
                _lifetime.Dispose();
            }
        }
    }

    private CommandContextItem SettingsContextItem() => new(_settings)
    {
        Title = Resources.OpenSettings,
        Subtitle = Resources.OpenSettingsDescription,
    };

    private string DisabledHint() => _catalog?.DisabledReason switch
    {
        "module-disabled" => _catalog?.IsPolicyControlled == true ? Resources.ModulePolicyDisabledHint : Resources.ModuleDisabledHint,
        "parent-policy-disabled" => Resources.ParentPolicyDisabledHint,
        "policy-disabled" or "integration-policy-disabled" => Resources.IntegrationPolicyDisabledHint,
        "integration-disabled" => Resources.IntegrationDisabledHint,
        _ => _catalog?.IsPolicyControlled == true ? Resources.IntegrationPolicyDisabledHint : Resources.IntegrationDisabledHint,
    };

    private void StartDiscovery()
    {
        lock (_gate)
        {
            if (_disposed || _discovering)
            {
                return;
            }

            _started = true;
            _discovering = true;
            _error = null;
            IsLoading = true;
            _discovery = Task.Run(DiscoverAsync);
        }

        RaiseItemsChanged();
    }

    private async Task DiscoverAsync()
    {
        try
        {
            var catalog = await _service.DiscoverAsync(_lifetime.Token).ConfigureAwait(false);
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _catalog = catalog;
                foreach (var script in catalog.Scripts)
                {
                    if (_pages.TryGetValue(script.Id, out var page))
                    {
                        page.UpdateScript(script);
                    }

                    if (_runCommands.TryGetValue(script.Id, out var runCommand))
                    {
                        runCommand.UpdateScript(script);
                    }
                }

                var ids = catalog.Scripts.Select(script => script.Id).ToHashSet(StringComparer.Ordinal);
                foreach (var id in _pages.Keys.Where(id => !ids.Contains(id) && !_sessions[id].IsRunning).ToArray())
                {
                    _pages[id].Dispose();
                    _sessions[id].Dispose();
                    _pages.Remove(id);
                    _sessions.Remove(id);
                    _runCommands.Remove(id);
                }
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            // Provider disposal is not a user-visible discovery failure.
        }
        catch (OperationCanceledException)
        {
            lock (_gate)
            {
                _error = Resources.DiscoveryTimedOut;
            }
        }
        catch (Exception ex)
        {
            lock (_gate)
            {
                _error = ex.Message;
            }
        }
        finally
        {
            lock (_gate)
            {
                _discovering = false;
                _lastDiscovery = DateTimeOffset.UtcNow;
                IsLoading = false;
                if (_disposed)
                {
                    _lifetime.Dispose();
                }
            }

            RaiseItemsChanged();
        }
    }
}
