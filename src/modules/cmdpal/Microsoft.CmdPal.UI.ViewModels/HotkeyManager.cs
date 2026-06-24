// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CmdPal.UI.ViewModels.Settings;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class HotkeyManager : ObservableObject
{
    private readonly TopLevelCommandManager _topLevelCommandManager;
    private readonly ISettingsService _settingsService;

    public HotkeyManager(TopLevelCommandManager tlcManager, ISettingsService settingsService)
    {
        _topLevelCommandManager = tlcManager;
        _settingsService = settingsService;
    }

    public void UpdateHotkey(string commandId, HotkeySettings? hotkey)
    {
        _settingsService.UpdateSettings(s =>
        {
            // Remove any command already bound to this hotkey, and remove old binding for this command
            var hotkeys = s.CommandHotkeys
                .RemoveAll(item => item.Hotkey == hotkey || item.CommandId == commandId);

            if (hotkey is not null)
            {
                hotkeys = hotkeys.Add(new(hotkey, commandId));
            }

            return s with { CommandHotkeys = hotkeys };
        });
    }
}
