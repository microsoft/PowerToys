// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.CmdPal.UI.ViewModels.Settings;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class HotkeyManager : ObservableObject
{
    private readonly TopLevelCommandManager _topLevelCommandManager;
    private readonly List<TopLevelHotkey> _commandHotkeys;

    public HotkeyManager(TopLevelCommandManager tlcManager, SettingsModel settings)
    {
        _topLevelCommandManager = tlcManager;
        _commandHotkeys = settings.CommandHotkeys;
    }

    public void UpdateHotkey(string commandId, HotkeySettings? hotkey)
    {
        // If any of the commands were already bound to this hotkey, remove that
        foreach (var item in _commandHotkeys)
        {
            if (item.Hotkey == hotkey)
            {
                item.Hotkey = null;
            }
        }

        _commandHotkeys.RemoveAll(item => item.Hotkey == null);

        foreach (var item in _commandHotkeys)
        {
            if (item.CommandId == commandId)
            {
                _commandHotkeys.Remove(item);
                break;
            }
        }

        _commandHotkeys.Add(new(hotkey, commandId));
    }
}
