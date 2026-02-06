// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Core.ViewModels;

public partial class DefaultContextMenuFactory : IContextMenuFactory
{
    public IEnumerable<IContextItemViewModel> UnsafeBuildAndInitMoreCommands(
        IContextItem[] items,
        CommandItemViewModel commandItem)
    {
        List<IContextItemViewModel> results = [];
        if (items is not null)
        {
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
        }

        // TODO! move to a factory or something, so that the UI layer is
        // responsible for stealth-adding this pin command.
        if (commandItem.PageContext.TryGetTarget(out var pageContext))
        {
            // * The extension needs to support the GetCommandItem API to support pinning
            // * We need to have an ID to pin it by
            // * We don't want to show "Pin to Dock" on items that are already context menu items
            if (pageContext.ExtensionSupportsPinning &&
                !string.IsNullOrEmpty(commandItem.Command.Id) &&
                !commandItem.IsContextMenuItem)
            {
                results.Add(new SeparatorViewModel());
                var pinCommand = new PinToDockIten(commandItem);
                var pinCommandViewModel = new CommandContextItemViewModel(pinCommand, commandItem.PageContext);
                pinCommandViewModel.SlowInitializeProperties();
                results.Add(pinCommandViewModel);
            }
        }

        return results;
    }

    private sealed partial class PinToDockIten : CommandContextItem
    {
        private static readonly PinToDockCommand PinCommand = new();
        private readonly CommandItemViewModel _owner;

        internal CommandItemViewModel Owner => _owner;

        public PinToDockIten(CommandItemViewModel owner)
            : base(PinCommand)
        {
            _owner = owner;
        }
    }

    private sealed partial class PinToDockCommand : InvokableCommand
    {
        public override string Name => "Toggle pinned to dock"; // TODO!LOC

        public PinToDockCommand()
        {
        }

        public override ICommandResult Invoke(object? sender)
        {
            if (sender is PinToDockIten contextItemViewModel)
            {
                // if (contextItemViewModel.PageContext.TryGetTarget(out var pageContext))
                // {
                //     pageContext.TogglePinnedToDock(contextItemViewModel);
                // }
                return CommandResult.ShowToast($"Attempted to toggle pin to dock for {contextItemViewModel.Owner.Title}");
            }

            return CommandResult.ShowToast($"Failed to get sender for command");
        }
    }
}
