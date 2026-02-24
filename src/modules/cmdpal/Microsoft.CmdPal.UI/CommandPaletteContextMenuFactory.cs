// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using RS_ = Microsoft.CmdPal.UI.Helpers.ResourceLoaderInstance;

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

        if (commandItem.PageContext.TryGetTarget(out var page) &&
            page.ProviderContext.SupportsPinning &&
            !string.IsNullOrEmpty(itemId))
        {
            // Add pin/unpin commands for pinning items to the top-level or to
            // the dock.
            var providerId = page.ProviderContext.ProviderId;
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
                var isTopLevelItem = page is TopLevelItemPageContext;

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

    internal enum PinLocation
    {
        TopLevel,
        Dock,
    }

    private sealed partial class PinToContextItem : CommandContextItem, IDisposable
    {
        private readonly PinToCommand _command;
        private readonly CommandItemViewModel _commandItem;

        public PinToContextItem(PinToCommand command, CommandItemViewModel commandItem)
            : base(command)
        {
            _command = command;
            _commandItem = commandItem;
            command.PinStateChanged += this.OnPinStateChanged;
        }

        private void OnPinStateChanged(object? sender, EventArgs e)
        {
            // update our MoreCommands
            _commandItem.RefreshMoreCommands();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            _command.PinStateChanged -= this.OnPinStateChanged;
        }
    }

    private sealed partial class PinToCommand : InvokableCommand
    {
        private readonly string _commandId;
        private readonly string _providerId;
        private readonly SettingsModel _settings;
        private readonly TopLevelCommandManager _topLevelCommandManager;
        private readonly bool _pin;
        private readonly PinLocation _pinLocation;

        public override IconInfo Icon => _pin ? Icons.PinIcon : Icons.UnpinIcon;

        public override string Name => _pin ? RS_.GetString("top_level_pin_command_name") : RS_.GetString("top_level_unpin_command_name");

        internal event EventHandler? PinStateChanged;

        public PinToCommand(
            string commandId,
            string providerId,
            bool pin,
            PinLocation pinLocation,
            SettingsModel settings,
            TopLevelCommandManager topLevelCommandManager)
        {
            _commandId = commandId;
            _providerId = providerId;
            _pinLocation = pinLocation;
            _settings = settings;
            _topLevelCommandManager = topLevelCommandManager;
            _pin = pin;
        }

        public override CommandResult Invoke()
        {
            CoreLogger.LogDebug($"PinTo{_pinLocation}Command.Invoke({_pin}): {_providerId}/{_commandId}");
            if (_pin)
            {
                switch (_pinLocation)
                {
                    case PinLocation.TopLevel:
                        PinToTopLevel();
                        break;

                        // TODO: After dock is added:
                        // case PinLocation.Dock:
                        //     PinToDock();
                        //     break;
                }
            }
            else
            {
                switch (_pinLocation)
                {
                    case PinLocation.TopLevel:
                        UnpinFromTopLevel();
                        break;

                        // case PinLocation.Dock:
                        //     UnpinFromDock();
                        //     break;
                }
            }

            PinStateChanged?.Invoke(this, EventArgs.Empty);

            return CommandResult.KeepOpen();
        }

        private void PinToTopLevel()
        {
            PinCommandItemMessage message = new(_providerId, _commandId);
            WeakReferenceMessenger.Default.Send(message);
        }

        private void UnpinFromTopLevel()
        {
            UnpinCommandItemMessage message = new(_providerId, _commandId);
            WeakReferenceMessenger.Default.Send(message);
        }
    }
}
