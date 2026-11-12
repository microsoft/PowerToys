// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using Microsoft.CmdPal.Ext.PowerScripts.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.PowerScripts;

/// <summary>
/// Provides a browser and top-level search results for compatible PowerScripts.
/// </summary>
public sealed partial class PowerScriptsCommandsProvider : CommandProvider
{
    private static readonly IconInfo PowerScriptsIcon = new("\uE756");

    private readonly PowerScriptsCatalogSource _source;
    private readonly PowerScriptsListPage _page;
    private readonly ICommandItem[] _commands;
    private readonly IFallbackCommandItem[] _fallbackCommands;
    private int _disposed;

    public PowerScriptsCommandsProvider(Action<IPage> navigate)
        : this(new PowerScriptsService(), navigate)
    {
    }

    internal PowerScriptsCommandsProvider(IPowerScriptsService service, Action<IPage> navigate)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(navigate);

        Id = "com.microsoft.cmdpal.builtin.powerscripts";
        DisplayName = Resources.PowerScripts;
        Icon = PowerScriptsIcon;

        _source = new PowerScriptsCatalogSource(service, navigate);
        _source.RefreshCompleted += CatalogRefreshed;
        _page = new PowerScriptsListPage(_source);
        _fallbackCommands = [new PowerScriptsFallback(_source, _page)];
        _commands =
        [
            new CommandItem(_page)
            {
                Title = DisplayName,
                Icon = PowerScriptsIcon,
            },
        ];
    }

    public override ICommandItem[] TopLevelCommands() => _commands;

    public override IFallbackCommandItem[] FallbackCommands() => _fallbackCommands;

    public override void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _source.RefreshCompleted -= CatalogRefreshed;
        _page.Dispose();
        _source.Dispose();
        base.Dispose();
    }

    private void CatalogRefreshed(object? sender, EventArgs args)
    {
        if (Volatile.Read(ref _disposed) == 0)
        {
            // Reload the source so an unchanged main-page query also receives
            // the refreshed catalogue. Initial discovery must not cause a reload loop.
            RaiseItemsChanged();
        }
    }
}
