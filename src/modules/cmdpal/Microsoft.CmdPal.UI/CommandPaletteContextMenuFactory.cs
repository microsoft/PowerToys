// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using ManagedCommon;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Settings;
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

    /// <summary>
    /// Constructs the view models for the MoreCommands of a
    /// CommandItemViewModel. In our case, we can use our settings to add a
    /// contextually-relevant pin/unpin command to this item.
    ///
    /// This is called on all CommandItemViewModels. There are however some
    /// weird edge cases we need to handle, concerning
    /// </summary>
    public List<IContextItemViewModel> UnsafeBuildAndInitMoreCommands(
        IContextItem[] items,
        CommandItemViewModel commandItem)
    {
        var results = DefaultContextMenuFactory.Instance.UnsafeBuildAndInitMoreCommands(items, commandItem);

        IPageContext? page = null;
        var succeeded = commandItem.PageContext.TryGetTarget(out page);
        if (!succeeded || page is null)
        {
            return results;
        }

        var isTopLevelItem = page is TopLevelItemPageContext;
        if (isTopLevelItem)
        {
            // Bail early. We'll handle it below.
            return results;
        }

        List<IContextItem> moreCommands = [];
        var itemId = commandItem.Command.Id;
        var providerContext = page.ProviderContext;
        var supportsPinning = providerContext.SupportsPinning;

        if (supportsPinning &&
            !string.IsNullOrEmpty(itemId))
        {
            // Add pin/unpin commands for pinning items to the top-level or to
            // the dock.
            var providerId = providerContext.ProviderId;
            if (_topLevelCommandManager.LookupProvider(providerId) is CommandProviderWrapper)
            {
                var alreadyPinnedToTopLevel = _settingsModel.IsCommandPinned(providerId, itemId);

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

                TryAddPinToDockCommand(itemId, providerId, moreCommands, commandItem);
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

    /// <summary>
    /// Called to create the context menu on TopLevelViewModels.
    ///
    /// These are handled differently from everyone else. With
    /// TopLevelViewModels, the ID isn't on the Command, it is on the
    /// TopLevelViewModel itself. Basically, we can't figure out how to add
    /// pin/unpin commands directly attached to the ICommandItems that we get
    /// from the API.
    ///
    /// Instead, this method is used to extend the set of IContextItems that are
    /// added to the TopLevelViewModel itself. This lets us pin/unpin the
    /// generated ID of the TopLevelViewModel, even if the command didn't have
    /// one.
    /// </summary>
    public void AddMoreCommandsToTopLevel(
        TopLevelViewModel topLevelItem,
        ICommandProviderContext providerContext,
        List<IContextItem?> contextItems)
    {
        var itemId = topLevelItem.Id;
        List<IContextItem> moreCommands = [];
        var commandItem = topLevelItem.ItemViewModel;

        // Add pin/unpin commands for pinning items to the top-level or to
        // the dock.
        var providerId = providerContext.ProviderId;
        var commandProvider = _topLevelCommandManager.LookupProvider(providerId);
        if (commandProvider is CommandProviderWrapper)
        {
            TryAddMovePinnedCommands(itemId, providerId, commandItem, moreCommands);
            TryAddUnpinFromHomeCommand(itemId, providerId, commandItem, moreCommands);
            TryAddPinToHomeCommand(itemId, providerId, commandItem, moreCommands);

            TryAddPinToDockCommand(itemId, providerId, moreCommands, commandItem);
        }

        if (moreCommands.Count > 0)
        {
            moreCommands.Insert(0, new Separator());

            // var moreResults = DefaultContextMenuFactory.Instance.UnsafeBuildAndInitMoreCommands(moreCommands.ToArray(), commandItem);
            contextItems.AddRange(moreCommands);
        }
    }

    private void TryAddPinToHomeCommand(
        string itemId,
        string providerId,
        CommandItemViewModel commandItem,
        List<IContextItem> moreCommands)
    {
        if (_settingsModel.IsCommandPinned(providerId, itemId))
        {
            return;
        }

        var pinToTopLevelCommand = new PinToCommand(
            commandId: itemId,
            providerId: providerId,
            pin: true,
            PinLocation.TopLevel,
            _settingsModel,
            _topLevelCommandManager);

        var contextItem = new PinToContextItem(pinToTopLevelCommand, commandItem);
        moreCommands.Add(contextItem);
    }

    private void TryAddUnpinFromHomeCommand(
        string itemId,
        string providerId,
        CommandItemViewModel commandItem,
        List<IContextItem> moreCommands)
    {
        var isPinnedSubCommand = _settingsModel.IsCommandPinned(providerId, itemId);
        if (isPinnedSubCommand)
        {
            var pinToTopLevelCommand = new PinToCommand(
                commandId: itemId,
                providerId: providerId,
                pin: !isPinnedSubCommand,
                PinLocation.TopLevel,
                _settingsModel,
                _topLevelCommandManager);

            var contextItem = new PinToContextItem(pinToTopLevelCommand, commandItem);
            moreCommands.Add(contextItem);
        }
    }

    private void TryAddMovePinnedCommands(
        string itemId,
        string providerId,
        CommandItemViewModel commandItem,
        List<IContextItem> moreCommands)
    {
        if (!_settingsModel.IsCommandPinned(providerId, itemId))
        {
            return;
        }

        var moveToTopCommand = new MovePinnedCommand(providerId, itemId, MovePinnedDirection.ToTop, _settingsModel, _topLevelCommandManager);
        moreCommands.Add(new MovePinnedContextItem(moveToTopCommand, commandItem));

        var moveUpCommand = new MovePinnedCommand(providerId, itemId, MovePinnedDirection.Up, _settingsModel, _topLevelCommandManager);
        moreCommands.Add(new MovePinnedContextItem(moveUpCommand, commandItem));

        var moveDownCommand = new MovePinnedCommand(providerId, itemId, MovePinnedDirection.Down, _settingsModel, _topLevelCommandManager);
        moreCommands.Add(new MovePinnedContextItem(moveDownCommand, commandItem));
    }

    private void TryAddPinToDockCommand(
        string itemId,
        string providerId,
        List<IContextItem> moreCommands,
        CommandItemViewModel commandItem)
    {
        if (!_settingsModel.EnableDock)
        {
            return;
        }

        var inStartBands = _settingsModel.DockSettings.StartBands.Any(band => MatchesBand(band, itemId, providerId));
        var inCenterBands = _settingsModel.DockSettings.CenterBands.Any(band => MatchesBand(band, itemId, providerId));
        var inEndBands = _settingsModel.DockSettings.EndBands.Any(band => MatchesBand(band, itemId, providerId));
        var alreadyPinned = inStartBands || inCenterBands || inEndBands; /** &&
                            _settingsModel.DockSettings.PinnedCommands.Contains(this.Id)**/
        var pinToTopLevelCommand = new PinToCommand(
            commandId: itemId,
            providerId: providerId,
            pin: !alreadyPinned,
            PinLocation.Dock,
            _settingsModel,
            _topLevelCommandManager);

        var contextItem = new PinToContextItem(pinToTopLevelCommand, commandItem);
        moreCommands.Add(contextItem);
    }

    internal static bool MatchesBand(DockBandSettings bandSettings, string commandId, string providerId)
    {
        return bandSettings.CommandId == commandId &&
               bandSettings.ProviderId == providerId;
    }

    internal enum PinLocation
    {
        TopLevel,
        Dock,
    }

    private sealed partial class PinToContextItem : CommandContextItem
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

        ~PinToContextItem()
        {
            _command.PinStateChanged -= this.OnPinStateChanged;
        }
    }

    private sealed partial class MovePinnedContextItem : CommandContextItem
    {
        private readonly MovePinnedCommand _command;
        private readonly CommandItemViewModel _commandItem;

        public MovePinnedContextItem(MovePinnedCommand command, CommandItemViewModel commandItem)
            : base(command)
        {
            _command = command;
            _commandItem = commandItem;
            command.MoveStateChanged += this.OnMoveStateChanged;
        }

        private void OnMoveStateChanged(object? sender, EventArgs e)
        {
            _commandItem.RefreshMoreCommands();
        }

        ~MovePinnedContextItem()
        {
            _command.MoveStateChanged -= this.OnMoveStateChanged;
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

        private bool IsPinToDock => _pinLocation == PinLocation.Dock;

        public override IconInfo Icon => _pin ? Icons.PinIcon : Icons.UnpinIcon;

        public override string Name => _pin ?
            (IsPinToDock ? RS_.GetString("dock_pin_command_name") : RS_.GetString("top_level_pin_command_name")) :
            (IsPinToDock ? RS_.GetString("dock_unpin_command_name") : RS_.GetString("top_level_unpin_command_name"));

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
            Logger.LogDebug($"PinTo{_pinLocation}Command.Invoke({_pin}): {_providerId}/{_commandId}");
            if (_pin)
            {
                switch (_pinLocation)
                {
                    case PinLocation.TopLevel:
                        PinToTopLevel();
                        break;

                    case PinLocation.Dock:
                        PinToDock();
                        break;
                }
            }
            else
            {
                switch (_pinLocation)
                {
                    case PinLocation.TopLevel:
                        UnpinFromTopLevel();
                        break;

                    case PinLocation.Dock:
                        UnpinFromDock();
                        break;
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

        private void PinToDock()
        {
            PinToDockMessage message = new(_providerId, _commandId, true);
            WeakReferenceMessenger.Default.Send(message);
        }

        private void UnpinFromDock()
        {
            PinToDockMessage message = new(_providerId, _commandId, false);
            WeakReferenceMessenger.Default.Send(message);
        }
    }

    private sealed partial class MovePinnedCommand : InvokableCommand
    {
        private readonly string _providerId;
        private readonly string _commandId;
        private readonly MovePinnedDirection _moveDirection;
        private readonly SettingsModel _settings;
        private readonly TopLevelCommandManager _topLevelCommandManager;

        public override IconInfo Icon => _moveDirection switch
        {
            MovePinnedDirection.ToTop => Icons.MoveToTopIcon,
            MovePinnedDirection.Up => Icons.MoveUpIcon,
            _ => Icons.MoveDownIcon,
        };

        public override string Name => _moveDirection switch
        {
            MovePinnedDirection.ToTop => RS_.GetString("top_level_move_to_top_command_name"),
            MovePinnedDirection.Up => RS_.GetString("top_level_move_up_command_name"),
            _ => RS_.GetString("top_level_move_down_command_name"),
        };

        internal event EventHandler? MoveStateChanged;

        public MovePinnedCommand(
            string providerId,
            string commandId,
            MovePinnedDirection moveDirection,
            SettingsModel settings,
            TopLevelCommandManager topLevelCommandManager)
        {
            _providerId = providerId;
            _commandId = commandId;
            _moveDirection = moveDirection;
            _settings = settings;
            _topLevelCommandManager = topLevelCommandManager;
        }

        public override CommandResult Invoke()
        {
            var moved = _moveDirection switch
            {
                MovePinnedDirection.ToTop => _settings.TryMovePinnedCommandToTop(_providerId, _commandId),
                MovePinnedDirection.Up => _settings.TryMovePinnedCommand(_providerId, _commandId, true, IsLoaded),
                _ => _settings.TryMovePinnedCommand(_providerId, _commandId, false, IsLoaded),
            };

            if (moved)
            {
                SettingsModel.SaveSettings(_settings, false);
                WeakReferenceMessenger.Default.Send<UpdateFallbackItemsMessage>();
                MoveStateChanged?.Invoke(this, EventArgs.Empty);
            }

            return CommandResult.KeepOpen();

            // Pass a visibility check so moves skip stale pinned entries
            // (removed/disabled/failed extensions) that aren't shown on home.
            bool IsLoaded(PinnedCommandSettings pin)
            {
                return _topLevelCommandManager.LookupCommand(pin.CommandId) is TopLevelViewModel cmd &&
                       cmd.CommandProviderId == pin.ProviderId;
            }
        }
    }

    private enum MovePinnedDirection
    {
        ToTop,
        Up,
        Down,
    }
}
