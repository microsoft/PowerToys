// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI;

internal sealed partial class CommandPaletteContextMenuFactory : IContextMenuFactory
{
    private readonly SettingsModel _settingsModel;
    private readonly TopLevelCommandManager _topLevelCommandManager;

    public CommandPaletteContextMenuFactory(SettingsModel settingsModel, TopLevelCommandManager topLevelCommandManager)
    {
        _settingsModel = settingsModel;
        _topLevelCommandManager = topLevelCommandManager;
    }

    public List<IContextItemViewModel> UnsafeBuildAndInitMoreCommands(
        IContextItem[] items,
        CommandItemViewModel commandItem)
    {
        var results = DefaultContextMenuFactory.Instance.UnsafeBuildAndInitMoreCommands(items, commandItem);

        // TODO: #45201 Here, we'll want to add pin/unpin commands for pinning
        // items to the top-level or to the dock.
        return results;
    }
}
