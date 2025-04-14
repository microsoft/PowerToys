// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.CmdPal.UI.ViewModels;

public class TopLevelViewModelFactory
{
    private readonly HotkeyManager _hotkeyManager;
    private readonly AliasManager _aliasManager;

    public TopLevelViewModelFactory(HotkeyManager hotkeyManager, AliasManager aliasManager)
    {
        _hotkeyManager = hotkeyManager;
        _aliasManager = aliasManager;
    }

    public TopLevelViewModel Create(
        CommandItemViewModel item,
        bool fallback,
        CommandPaletteHost host,
        string providerId,
        SettingsModel settings)
    {
        return new TopLevelViewModel(item, fallback, host, providerId, settings, _hotkeyManager, _aliasManager);
    }
}
