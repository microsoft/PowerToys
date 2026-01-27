// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.CmdPal.UI.ViewModels.Settings;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class HotkeyManager : ObservableObject
{
    private readonly TopLevelCommandManager _topLevelCommandManager;
    private readonly SettingsService _settingsService;

    private List<TopLevelHotkey> CommandHotkeys => _settingsService.CurrentSettings.CommandHotkeys;

    public HotkeyManager(TopLevelCommandManager tlcManager, SettingsService settingsService)
    {
        _topLevelCommandManager = tlcManager;
        _settingsService = settingsService;
    }

    public void UpdateHotkey(string commandId, HotkeySettings? hotkey)
    {
        // If any of the commands were already bound to this hotkey, remove that
        foreach (var item in CommandHotkeys)
        {
            if (item.Hotkey == hotkey)
            {
                item.Hotkey = null;
            }
        }

        CommandHotkeys.RemoveAll(item => item.Hotkey is null);

        foreach (var item in CommandHotkeys)
        {
            if (item.CommandId == commandId)
            {
                CommandHotkeys.Remove(item);
                break;
            }
        }

        CommandHotkeys.Add(new(hotkey, commandId));
    }
}
