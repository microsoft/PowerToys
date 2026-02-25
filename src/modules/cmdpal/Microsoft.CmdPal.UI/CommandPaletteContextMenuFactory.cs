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

        List<IContextItem> moreCommands = [];
        var itemId = commandItem.Command.Id;
        IPageContext? page = null;
        var succeeded = commandItem.PageContext.TryGetTarget(out page);
        if (!succeeded || page is null)
        {
            return results;
        }

        var isTopLevelItem = page is TopLevelItemPageContext;
        var providerContext = page.ProviderContext;
        var supportsPinning = providerContext.SupportsPinning;

        if ((isTopLevelItem || supportsPinning) &&
            !string.IsNullOrEmpty(itemId))
        {
            // Add pin/unpin commands for pinning items to the top-level or to
            // the dock.
            var providerId = providerContext.ProviderId;
            if (_topLevelCommandManager.LookupProvider(providerId) is CommandProviderWrapper provider)
            {
                var providerSettings = _settingsModel.GetProviderSettings(provider);

                var alreadyPinnedToTopLevel = providerSettings.PinnedCommandIds.Contains(itemId);

                // Don't add pin/unpin commands for items displayed as
                // TopLevelViewModels that aren't already pinned.
                //
                // We can't look up if this command item is in the top level
                // items in the manager, because we are being called _before_ we
                // get added to the manager's list of commands.
                if (!isTopLevelItem || alreadyPinnedToTopLevel)
                {
                    var pinToTopLevelCommand = new PinToCommand(
                        commandId: itemId,
                        providerId: providerId,
                        pin: !alreadyPinnedToTopLevel,
                        PinLocation.TopLevel,
                        _settingsModel,
                        _topLevelCommandManager);

                    var contextItem = new PinToContextItem(pinToTopLevelCommand, commandItem);
                    moreCommands.Add(contextItem);
                }

                TryAddPinToDockCommand(providerSettings, itemId, providerId, moreCommands, commandItem);
            }
        }

        if (moreCommands.Count > 0)
        {
            moreCommands.Insert(0, new Separator());
            var moreResults = DefaultContextMenuFactory.Instance.UnsafeBuildAndInitMoreCommands(moreCommands.ToArray(), commandItem);
            results.AddRange(moreResults);
        }

        return results;
    }
}
