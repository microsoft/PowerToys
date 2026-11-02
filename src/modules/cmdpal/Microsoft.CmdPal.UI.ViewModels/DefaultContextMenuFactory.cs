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
        CommandItemViewModel commandItem) => UnsafeBuildAndInitMoreCommands(items, commandItem.PageContext);

    internal List<IContextItemViewModel> UnsafeBuildAndInitMoreCommands(
        IContextItem[] items,
        WeakReference<IPageContext> pageContext)
    {
        List<IContextItemViewModel> results = [];
        if (items is null)
        {
            return results;
        }

        try
        {
            foreach (var item in items)
            {
                if (item is ICommandContextItem contextItem)
                {
                    var contextItemViewModel = new CommandContextItemViewModel(contextItem, pageContext);
                    results.Add(contextItemViewModel);
                    contextItemViewModel.SlowInitializeProperties();
                }
                else
                {
                    results.Add(new SeparatorViewModel());
                }
            }
        }
        catch
        {
            CleanupCommands(results);
            throw;
        }

        return results;
    }

    internal static void CleanupCommands(IEnumerable<IContextItemViewModel> commands)
    {
        foreach (var command in commands.OfType<CommandContextItemViewModel>())
        {
            command.SafeCleanup();
        }
    }

    public void AddMoreCommandsToTopLevel(
        TopLevelViewModel topLevelItem,
        ICommandProviderContext providerContext,
        List<IContextItem?> contextItems)
    {
        // do nothing
    }

    public void AddMoreCommandsToPage(PageViewModel page, List<IContextItem> contextItems)
    {
        // The default factory only uses commands supplied by the extension.
    }
}
