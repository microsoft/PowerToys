// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.PowerScripts.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.PowerScripts;

internal sealed partial class PowerScriptsListPage : DynamicListPage, IDisposable
{
    private readonly object _gate = new();
    private readonly PowerScriptsCatalogSource _source;
    private readonly bool _ownsSource;
    private readonly AnonymousCommand _refresh;
    private readonly OpenPowerScriptsSettingsCommand _settings = new();
    private bool _disposed;

    internal PowerScriptsListPage(IPowerScriptsService service, Action<IPage> navigate)
        : this(new PowerScriptsCatalogSource(service, navigate), ownsSource: true)
    {
    }

    internal PowerScriptsListPage(PowerScriptsCatalogSource source)
        : this(source, ownsSource: false)
    {
    }

    private PowerScriptsListPage(PowerScriptsCatalogSource source, bool ownsSource)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
        _ownsSource = ownsSource;
        Id = "com.microsoft.cmdpal.builtin.powerscripts.list";
        Name = Resources.Open;
        Title = Resources.PowerScripts;
        Icon = new IconInfo("\uE756");
        PlaceholderText = Resources.SearchPlaceholder;
        _refresh = new AnonymousCommand(_source.Refresh)
        {
            Id = "com.microsoft.cmdpal.builtin.powerscripts.refresh",
            Name = Resources.Refresh,
            Icon = new IconInfo("\uE72C"),
            Result = CommandResult.KeepOpen(),
        };
        IsLoading = _source.IsDiscovering;
        _source.Changed += SourceChanged;
    }

    public override void UpdateSearchText(string oldSearch, string newSearch) => RaiseItemsChanged();

    public override IListItem[] GetItems()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return [];
            }
        }

        _source.EnsureDiscovery();
        lock (_gate)
        {
            if (_disposed)
            {
                return [];
            }

            IsLoading = _source.IsDiscovering;
            if (IsLoading)
            {
                EmptyContent = new CommandItem(new NoOpCommand())
                {
                    Title = Resources.Loading,
                    Subtitle = Resources.LoadingDescription,
                    Icon = Icon,
                };
                return [];
            }

            if (_source.Error is { } error)
            {
                EmptyContent = new CommandItem(_refresh)
                {
                    Title = Resources.DiscoveryFailed,
                    Subtitle = error,
                    Icon = Icon,
                    MoreCommands = [SettingsContextItem()],
                };
                return [];
            }

            var catalog = _source.Catalog;
            if (catalog is null || !catalog.IsEnabled)
            {
                EmptyContent = new CommandItem(_settings)
                {
                    Title = catalog?.IsPolicyControlled == true ? Resources.DisabledByPolicy : Resources.Disabled,
                    Subtitle = DisabledHint(catalog),
                    Icon = Icon,
                    MoreCommands = [new CommandContextItem(_refresh) { Title = Resources.Refresh }],
                };
                return [];
            }

            var items = _source.GetItems(catalog, SearchText);
            if (items.Length == 0)
            {
                var empty = catalog.Scripts.Count == 0;
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
            return items;
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
        }

        _source.Changed -= SourceChanged;
        if (_ownsSource)
        {
            _source.Dispose();
        }
    }

    private CommandContextItem SettingsContextItem() => new(_settings)
    {
        Title = Resources.OpenSettings,
        Subtitle = Resources.OpenSettingsDescription,
    };

    private static string DisabledHint(PowerScriptsCatalog? catalog) => catalog?.DisabledReason switch
    {
        "module-disabled" => catalog?.IsPolicyControlled == true ? Resources.ModulePolicyDisabledHint : Resources.ModuleDisabledHint,
        "parent-policy-disabled" => Resources.ParentPolicyDisabledHint,
        "policy-disabled" or "integration-policy-disabled" => Resources.IntegrationPolicyDisabledHint,
        "integration-disabled" => Resources.IntegrationDisabledHint,
        _ => catalog?.IsPolicyControlled == true ? Resources.IntegrationPolicyDisabledHint : Resources.IntegrationDisabledHint,
    };

    private void SourceChanged(object? sender, EventArgs args)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            IsLoading = _source.IsDiscovering;
        }

        RaiseItemsChanged();
    }
}
