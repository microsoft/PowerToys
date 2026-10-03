// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Commands;
using PowerToysExtension.Helpers;
using PowerToysExtension.Pages;
using PowerToysExtension.Properties;

namespace PowerToysExtension;

public partial class PowerToysExtensionCommandsProvider : CommandProvider
{
    private readonly ICommandItem[] _commands;
    private readonly IPowerDisplayCliService _powerDisplayCliService;
    private readonly CancellationTokenSource _lifetimeCancellationSource = new();
    private readonly CancellationToken _lifetimeCancellationToken;
    private PowerDisplayProfileCommandResolver _powerDisplayProfileResolver;
    private int _disposed;

    public PowerToysExtensionCommandsProvider()
        : this(new PowerDisplayCliService())
    {
    }

    internal PowerToysExtensionCommandsProvider(IPowerDisplayCliService powerDisplayCliService)
    {
        _powerDisplayCliService = powerDisplayCliService ?? throw new ArgumentNullException(nameof(powerDisplayCliService));
        _lifetimeCancellationToken = _lifetimeCancellationSource.Token;
        _powerDisplayProfileResolver = new(_powerDisplayCliService, _lifetimeCancellationToken);
        DisplayName = Resources.PowerToys_DisplayName;
        Icon = PowerToysResourcesHelper.ProviderIcon();
        _commands = [
            new CommandItem(new Pages.PowerToysListPage())
            {
                Title = Resources.PowerToys_DisplayName,
                Subtitle = Resources.PowerToys_Subtitle,
            },
        ];

        SettingsChangeNotifier.SettingsChanged += RaiseModuleItemsChanged;
        KeyboardManagerStateService.StatusChanged += RaiseModuleItemsChanged;
    }

    public override ICommandItem[] TopLevelCommands()
    {
        // The host restores pinned commands and dock items after requesting top-level commands.
        // Share their metadata query for this load, without retaining it across provider reloads.
        // Overlapping host reloads can still hold items from the previous batch. Let their
        // queries finish as well; all batches share the extension's shutdown token.
        Volatile.Write(ref _powerDisplayProfileResolver, new(_powerDisplayCliService, _lifetimeCancellationToken));
        return _commands;
    }

    public override void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            SettingsChangeNotifier.SettingsChanged -= RaiseModuleItemsChanged;
            KeyboardManagerStateService.StatusChanged -= RaiseModuleItemsChanged;
            _lifetimeCancellationSource.Cancel();
            _lifetimeCancellationSource.Dispose();
            base.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    public override IFallbackCommandItem[] FallbackCommands()
    {
        var items = ModuleCommandCatalog.GetAllItems();
        var fallbacks = new List<IFallbackCommandItem>(items.Length);
        foreach (var item in items)
        {
            if (item?.Command is not ICommand cmd)
            {
                continue;
            }

            fallbacks.Add(new PowerToysFallbackCommandItem(cmd, item.Title, item.Subtitle, item.Icon, item.MoreCommands));
        }

        return fallbacks.ToArray();
    }

    public override ICommandItem? GetCommandItem(string id)
    {
        // First check top-level commands.
        var allCommands = ModuleCommandCatalog.GetAllItems();
        foreach (var li in allCommands)
        {
            if (li?.Command is ICommand cmd && cmd.Id == id)
            {
                return li;
            }
        }

        return TryGetFancyZonesCommandItem(id) ?? TryGetPowerDisplayProfileCommandItem(id);
    }

    private void RaiseModuleItemsChanged()
    {
        RaiseItemsChanged();
    }

    private static ICommandItem? TryGetFancyZonesCommandItem(string id)
    {
        if (!FancyZonesCommandIds.TryParseApplyLayoutCommandId(id, out var layoutId, out var monitorToken))
        {
            return null;
        }

        var layout = FancyZonesDataService.GetLayouts()
            .FirstOrDefault(candidate => string.Equals(candidate.Id, layoutId, StringComparison.Ordinal));
        if (layout is null)
        {
            return null;
        }

        var fallbackIcon = PowerToysResourcesHelper.IconFromSettingsIcon("FancyZones.png");
        if (string.IsNullOrWhiteSpace(monitorToken))
        {
            FancyZonesDataService.TryGetMonitors(out var monitors, out _);
            return new FancyZonesLayoutListItem(new ApplyFancyZonesLayoutCommand(layout, monitor: null), layout, fallbackIcon)
            {
                MoreCommands = FancyZonesContextHelper.BuildLayoutContext(layout, monitors),
            };
        }

        if (!FancyZonesDataService.TryGetMonitors(out var availableMonitors, out _))
        {
            return null;
        }

        var monitor = availableMonitors
            .FirstOrDefault(candidate => string.Equals(FancyZonesCommandIds.GetMonitorToken(candidate), monitorToken, StringComparison.Ordinal));

        if (monitor.Equals(default(FancyZonesMonitorDescriptor)))
        {
            return null;
        }

        return new FancyZonesLayoutListItem(new ApplyFancyZonesLayoutCommand(layout, monitor), layout, fallbackIcon)
        {
            Subtitle = FancyZonesContextHelper.FormatApplyToMonitorTitle(monitor),
        };
    }

    private ICommandItem? TryGetPowerDisplayProfileCommandItem(string id)
    {
        if (!PowerDisplayCommandIds.TryParseApplyProfileCommandId(id, out var profileId) ||
            !ModuleEnablementService.IsKeyEnabled("PowerDisplay"))
        {
            return null;
        }

        return Volatile.Read(ref _powerDisplayProfileResolver).GetCommandItem(profileId);
    }
}
