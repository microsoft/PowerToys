// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using ManagedCommon;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CmdPal.UI.ViewModels.Settings;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class HotkeyManager : ObservableObject, IDisposable
{
    private readonly TopLevelCommandManager _topLevelCommandManager;
    private readonly ISettingsService _settingsService;
    private readonly TaskScheduler _uiScheduler;
    private readonly Lock _settingsLock = new();
    private SettingsModel? _lastSettings;
    private int _refreshPending;
    private volatile bool _disposed;

    [ObservableProperty]
    public partial ImmutableList<TopLevelHotkey> ActiveCommandHotkeys { get; private set; } = [];

    public HotkeyManager(TopLevelCommandManager tlcManager, ISettingsService settingsService, TaskScheduler uiScheduler)
    {
        _topLevelCommandManager = tlcManager;
        _settingsService = settingsService;
        _uiScheduler = uiScheduler;

        _topLevelCommandManager.TopLevelCommands.CollectionChanged += Commands_CollectionChanged;
        _settingsService.SettingsChanged += SettingsService_SettingsChanged;
        RefreshActiveHotkeys();
    }

    private void Commands_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        QueueRefresh();
    }

    private void SettingsService_SettingsChanged(ISettingsService sender, SettingsModel settings)
    {
        lock (_settingsLock)
        {
            // Notifications can arrive out of order; compare the current collections together
            settings = sender.Settings;
            if (ReferenceEquals(_lastSettings?.CommandHotkeys, settings.CommandHotkeys)
                && ReferenceEquals(_lastSettings?.ProviderSettings, settings.ProviderSettings))
            {
                return;
            }

            _lastSettings = settings;
        }

        QueueRefresh();
    }

    private void QueueRefresh()
    {
        if (_disposed || Interlocked.Exchange(ref _refreshPending, 1) != 0)
        {
            return;
        }

        // Collection events can run under the loader's lock;
        // publish on the UI scheduler after it returns.
        _ = Task.Factory.StartNew(
            () =>
            {
                Interlocked.Exchange(ref _refreshPending, 0);
                if (!_disposed)
                {
                    RefreshActiveHotkeys();
                }
            },
            CancellationToken.None,
            TaskCreationOptions.DenyChildAttach,
            _uiScheduler);
    }

    private void RefreshActiveHotkeys()
    {
        try
        {
            RefreshActiveHotkeysCore();
        }
        catch (Exception ex)
        {
            lock (_settingsLock)
            {
                _lastSettings = null;
            }

            Logger.LogError("Failed to refresh active command hotkeys", ex);
        }
    }

    private void RefreshActiveHotkeysCore()
    {
        SettingsModel settings;
        lock (_settingsLock)
        {
            settings = _settingsService.Settings;
            _lastSettings = settings;
        }

        TopLevelViewModel[] commands;
        lock (_topLevelCommandManager.TopLevelCommands)
        {
            commands = [.. _topLevelCommandManager.TopLevelCommands];
        }

        var commandsById = new Dictionary<string, TopLevelViewModel>(StringComparer.Ordinal);
        foreach (var command in commands)
        {
            commandsById.TryAdd(command.Id, command);
        }

        ImmutableList<TopLevelHotkey> hotkeys =
        [
            .. settings.CommandHotkeys.Where(hotkey =>
            {
                if (string.IsNullOrEmpty(hotkey.CommandId) || !commandsById.TryGetValue(hotkey.CommandId, out var command))
                {
                    return false;
                }

                // Loaded commands retain a settings snapshot, so check the current settings.
                if (!TopLevelCommandManager.IsProviderEnabled(settings, command.CommandProviderId, out var providerSettings))
                {
                    return false;
                }

                return providerSettings is null
                       || command is not TopLevelFallbackViewModel fallback
                       || fallback.IsEnabledWithSettings(providerSettings);
            })
        ];

        if (!ActiveCommandHotkeys.SequenceEqual(hotkeys))
        {
            ActiveCommandHotkeys = hotkeys;
        }

        var hotkeysByCommandId = settings.CommandHotkeys.ToLookup(binding => binding.CommandId, StringComparer.Ordinal);
        foreach (var command in commands)
        {
            var commandId = command.Id;
            if (command.Hotkey != SettingsModel.GetCommandHotkey(hotkeysByCommandId[commandId], commandId))
            {
                command.RefreshHotkey();
            }
        }
    }

    public void UpdateHotkey(string commandId, HotkeySettings? hotkey)
    {
        _settingsService.UpdateSettings(s =>
        {
            var hotkeys = RemoveHotkeyForSameCommandIdOrKeyCombination(s);

            if (hotkey is not null && !hotkey.IsEmpty())
            {
                hotkeys = hotkeys.Add(new TopLevelHotkey(hotkey, commandId));
            }

            return s with { CommandHotkeys = hotkeys };
        });
        return;

        ImmutableList<TopLevelHotkey> RemoveHotkeyForSameCommandIdOrKeyCombination(SettingsModel s)
        {
            return s
                .CommandHotkeys
                .RemoveAll(item => item.CommandId == commandId || item.Hotkey?.HasSameKeyCombination(hotkey) == true);
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _topLevelCommandManager.TopLevelCommands.CollectionChanged -= Commands_CollectionChanged;
        _settingsService.SettingsChanged -= SettingsService_SettingsChanged;
        GC.SuppressFinalize(this);
    }
}
