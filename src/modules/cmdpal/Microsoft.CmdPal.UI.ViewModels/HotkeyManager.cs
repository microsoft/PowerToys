// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.CmdPal.UI.ViewModels.Settings;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class HotkeyManager : ObservableObject, IDisposable
{
    private readonly SettingsService _settingsService;
    private ImmutableList<TopLevelHotkey> _commandHotkeys;

    public HotkeyManager(SettingsService settingsService)
    {
        _settingsService = settingsService;
        _commandHotkeys = settingsService.CurrentSettings.CommandHotkeys;

        _settingsService.SettingsChanged += SettingsService_SettingsChanged;
    }

    private void SettingsService_SettingsChanged(SettingsService sender, Services.SettingsChangedEventArgs args)
    {
        _commandHotkeys = args.NewSettingsModel.CommandHotkeys;
    }

    public void UpdateHotkey(string commandId, HotkeySettings? hotkey)
    {
        // If any of the commands were already bound to this hotkey, remove that
        TopLevelHotkey? existingItem = null;

        foreach (var item in _commandHotkeys)
        {
            if (item.Hotkey == hotkey)
            {
                existingItem = item;
            }
        }

        if (existingItem is not null)
        {
            existingItem = existingItem with { Hotkey = null };
        }

        var newCommandHotkeys = _commandHotkeys.ToList();
        newCommandHotkeys.RemoveAll(item => item.Hotkey is null);

        foreach (var item in newCommandHotkeys)
        {
            if (item.CommandId == commandId)
            {
                newCommandHotkeys.Remove(item);
                break;
            }
        }

        newCommandHotkeys.Add(new(hotkey, commandId));

        _settingsService.SaveSettings(
            _settingsService.CurrentSettings with
            {
                CommandHotkeys = newCommandHotkeys.ToImmutableList<TopLevelHotkey>(),
            });
    }

    public void Dispose()
    {
        _settingsService.SettingsChanged -= SettingsService_SettingsChanged;
        GC.SuppressFinalize(this);
    }
}
