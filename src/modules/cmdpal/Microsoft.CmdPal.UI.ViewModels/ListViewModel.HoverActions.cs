// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ListViewModel
{
    public bool EnableListHoverActions => _settingsService?.Settings.EnableListHoverActions ?? true;

    public HoverActionSettings GetHoverActionSettings(ListItemViewModel row)
    {
        var mode = HoverActionsMode.Default;
        var max = -1;
        var visibility = HoverActionsVisibility.Default;

        if (TryGetCommandProvider() is ICommandProvider5 provider5)
        {
            mode = provider5.DefaultHoverActionsMode;
            max = provider5.DefaultMaxHoverActions;
        }

        if (_model.Unsafe is IListPage2 listPage2)
        {
            if (listPage2.HoverActionsMode != HoverActionsMode.Default)
            {
                mode = listPage2.HoverActionsMode;
            }

            if (listPage2.MaxHoverActions > 0)
            {
                max = listPage2.MaxHoverActions;
            }

            if (listPage2.HoverActionsVisibility != HoverActionsVisibility.Default)
            {
                visibility = listPage2.HoverActionsVisibility;
            }
        }

        if (IsMainPage && row.Model.Unsafe is TopLevelViewModel topLevel)
        {
            var commandItem = topLevel.ItemViewModel.Model.Unsafe;
            if (commandItem is ICommandItem2 commandItem2)
            {
                if (commandItem2.HomeHoverActionsMode != HoverActionsMode.Default)
                {
                    mode = commandItem2.HomeHoverActionsMode;
                }

                if (commandItem2.HomeMaxHoverActions > 0)
                {
                    max = commandItem2.HomeMaxHoverActions;
                }
            }
        }

        return new HoverActionSettings(mode, max, visibility);
    }

    public void RefreshAllHoverActions()
    {
        foreach (var item in Items)
        {
            item.RefreshHoverActions();
        }

        foreach (var item in FilteredItems)
        {
            item.RefreshHoverActions();
        }
    }

    private ICommandProvider? TryGetCommandProvider()
    {
        if (ExtensionHost is CommandPaletteHost commandPaletteHost)
        {
            return commandPaletteHost.CommandProvider;
        }

        return null;
    }

    private void SettingsService_SettingsChanged(ISettingsService sender, SettingsModel settings)
    {
        RefreshAllHoverActions();
    }
}
