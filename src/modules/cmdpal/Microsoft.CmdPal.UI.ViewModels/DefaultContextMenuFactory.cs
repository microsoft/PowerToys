// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class DefaultContextMenuFactory : IContextMenuFactory
{
    public static readonly DefaultContextMenuFactory Instance = new();

    private DefaultContextMenuFactory()
    {
    }

    public List<IContextItemViewModel> UnsafeBuildAndInitMoreCommands(
        IContextItem[] items,
        CommandItemViewModel commandItem)
    {
        List<IContextItemViewModel> results = [];
        if (items is null)
        {
            return results;
        }

        foreach (var item in items)
        {
            if (item is ICommandContextItem contextItem)
            {
                var contextItemViewModel = new CommandContextItemViewModel(contextItem, commandItem.PageContext);
                contextItemViewModel.SlowInitializeProperties();
                results.Add(contextItemViewModel);
            }
            else
            {
                results.Add(new SeparatorViewModel());
            }
        }

        return results;
    }

    public void AddMoreCommandsToTopLevel(
        TopLevelViewModel topLevelItem,
        ICommandProviderContext providerContext,
        List<IContextItem?> contextItems)
    {
        // do nothing
    }
}
