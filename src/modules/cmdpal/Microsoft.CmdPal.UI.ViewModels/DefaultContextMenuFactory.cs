// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Commands;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class DefaultContextMenuFactory : IContextMenuFactory
{
    public static readonly DefaultContextMenuFactory Instance = new();

    private DefaultContextMenuFactory()
    {
    }

    public List<IContextItemViewModel> UnsafeBuildAndInitMoreCommands(
        IContextItem[] items,
        CommandItemViewModel commandItem,
        ContextMenuPlacement placement)
    {
        List<IContextItemViewModel> results = [];
        if (items is not null)
        {
            foreach (var item in items)
            {
                if (item is ICommandContextItem contextItem)
                {
                    var contextItemViewModel = new CommandContextItemViewModel(contextItem, commandItem.PageContext, placement);
                    contextItemViewModel.SlowInitializeProperties();
                    results.Add(contextItemViewModel);
                }
                else
                {
                    results.Add(new SeparatorViewModel());
                }
            }
        }

        var showDetailsCommand = TryBuildShowDetailsCommand(results, commandItem, placement);
        if (showDetailsCommand is not null)
        {
            results.Add(showDetailsCommand);
        }

        return results;
    }

    public List<IContextItemViewModel>? UpdateMoreCommandsForDetails(
        IReadOnlyList<IContextItemViewModel> items,
        CommandItemViewModel commandItem,
        ContextMenuPlacement placement)
    {
        var showDetailsCommand = TryBuildShowDetailsCommand(items, commandItem, placement);
        if (showDetailsCommand is null && !ContainsSynthesizedShowDetails(items))
        {
            return null;
        }

        List<IContextItemViewModel> results = new(items.Count + 1);
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (!IsSynthesizedShowDetails(item))
            {
                results.Add(item);
            }
        }

        if (showDetailsCommand is not null)
        {
            results.Add(showDetailsCommand);
        }

        return results;
    }

    private static CommandContextItemViewModel? TryBuildShowDetailsCommand(
        IReadOnlyList<IContextItemViewModel> items,
        CommandItemViewModel commandItem,
        ContextMenuPlacement placement)
    {
        if (!placement.SupportsDetailsPane ||
            commandItem is not ListItemViewModel { Details: { } details } listItem ||
            !listItem.PageContext.TryGetTarget(out var pageContext) ||
            pageContext is not ListViewModel { ShowDetails: false } ||
            HasForeignShowDetails(items))
        {
            return null;
        }

        var command = new ShowDetailsCommand(details);
        var contextItem = new CommandContextItem(command)
        {
            Icon = command.Icon,
        };
        var viewModel = new CommandContextItemViewModel(contextItem, listItem.PageContext, placement);
        viewModel.SlowInitializeProperties();
        return viewModel;
    }

    private static bool ContainsSynthesizedShowDetails(IReadOnlyList<IContextItemViewModel> items)
    {
        for (var i = 0; i < items.Count; i++)
        {
            if (IsSynthesizedShowDetails(items[i]))
            {
                return true;
            }
        }

        return false;
    }

    // Detect by type check and reserved ID
    private static bool HasForeignShowDetails(IReadOnlyList<IContextItemViewModel> items)
    {
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i] is CommandContextItemViewModel contextItem &&
                !IsSynthesizedShowDetails(contextItem) &&
                contextItem.Command.Id == ShowDetailsCommand.ShowDetailsCommandId)
            {
                return true;
            }
        }

        return false;
    }

    // Matched by type, so we only ever replace or remove the command we synthesized.
    private static bool IsSynthesizedShowDetails(IContextItemViewModel item)
    {
        return item is CommandContextItemViewModel { Command.Model.Unsafe: ShowDetailsCommand };
    }

    public void AddMoreCommandsToTopLevel(
        TopLevelViewModel topLevelItem,
        ICommandProviderContext providerContext,
        List<IContextItem?> contextItems)
    {
        // do nothing
    }
}
