// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using ManagedCommon;
using Microsoft.CmdPal.UI.Messages;
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
        List<IContextItem> settingsCommands = [];
        List<IContextItem> pinningCommands = [];
        var commandItem = topLevelItem.ItemViewModel;
        var hasPrimaryCommand = !string.IsNullOrEmpty(commandItem.Command.Name);

        // Add pin/unpin commands for pinning items to the top-level or to
        // the dock.
        var providerId = providerContext.ProviderId;
        if (_topLevelCommandManager.LookupProvider(providerId) is CommandProviderWrapper provider)
        {
            var providerSettings = _settingsModel.GetProviderSettings(provider);
            var canOpenCommandSettings = !topLevelItem.IsFallback && !topLevelItem.IsDockBand;
            var replacedProviderSettingsCommand = ReplaceProviderSettingsCommands(provider, _settingsModel, contextItems);

            TryAddProviderSettingsCommand(provider, _settingsModel, contextItems, settingsCommands, replacedProviderSettingsCommand);

            if (canOpenCommandSettings)
            {
                settingsCommands.Add(new CommandContextItem(new OpenCommandAliasSettingsCommand(provider, _settingsModel, itemId)));
                settingsCommands.Add(new CommandContextItem(new OpenCommandGlobalHotkeySettingsCommand(provider, _settingsModel, itemId)));
            }

            var isPinnedSubCommand = providerSettings.PinnedCommandIds.Contains(itemId);
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
                pinningCommands.Add(contextItem);
            }

            TryAddPinToDockCommand(providerSettings, itemId, providerId, pinningCommands, commandItem);
        }

        AppendContextItemGroup(contextItems, settingsCommands, hasPrimaryCommand);
        AppendContextItemGroup(contextItems, pinningCommands, hasPrimaryCommand);
    }

    private static void TryAddProviderSettingsCommand(
        CommandProviderWrapper provider,
        SettingsModel settingsModel,
        IEnumerable<IContextItem?> contextItems,
        List<IContextItem> moreCommands,
        bool providerSettingsCommandAlreadyHandled)
    {
        if (providerSettingsCommandAlreadyHandled)
        {
            return;
        }

        var settingsPage = provider.Settings?.SettingsPageCommand;
        if (settingsPage is null || HasSettingsPageCommand(contextItems, settingsPage))
        {
            return;
        }

        moreCommands.Add(new CommandContextItem(new OpenExtensionSettingsPageCommand(provider, settingsModel)));
    }

    private static bool ReplaceProviderSettingsCommands(
        CommandProviderWrapper provider,
        SettingsModel settingsModel,
        IList<IContextItem?> contextItems)
    {
        var settingsPage = provider.Settings?.SettingsPageCommand;
        if (settingsPage is null)
        {
            return false;
        }

        var replacedAny = false;
        for (var i = 0; i < contextItems.Count; i++)
        {
            if (contextItems[i] is not ICommandContextItem contextItem)
            {
                continue;
            }

            try
            {
                if (contextItem.Command is IContentPage page &&
                    AreSameSettingsPage(page, settingsPage))
                {
                    contextItems[i] = CreateReplacementSettingsContextItem(provider, settingsModel, contextItem);
                    replacedAny = true;
                }
            }
            catch (Exception ex)
            {
                // Extension object may be unavailable.
                Logger.LogError($"Failed to check settings page for replacement", ex);
            }
        }

        return replacedAny;
    }

    private static CommandContextItem CreateReplacementSettingsContextItem(
        CommandProviderWrapper provider,
        SettingsModel settingsModel,
        ICommandContextItem source) => new(new OpenExtensionSettingsPageCommand(provider, settingsModel))
    {
        IsCritical = source.IsCritical,
        RequestedShortcut = source.RequestedShortcut,
        Title = source.Title,
        Subtitle = source.Subtitle,
        Icon = source.Icon,
    };

    private static bool HasSettingsPageCommand(IEnumerable<IContextItem?> contextItems, IContentPage settingsPage)
    {
        foreach (var item in contextItems)
        {
            if (item is ICommandContextItem contextItem &&
                contextItem.Command is IContentPage page &&
                AreSameSettingsPage(page, settingsPage))
            {
                return true;
            }
        }

        return false;
    }

    private static bool AreSameSettingsPage(IContentPage existingPage, IContentPage settingsPage)
    {
        if (ReferenceEquals(existingPage, settingsPage) || Equals(existingPage, settingsPage))
        {
            return true;
        }

        if (!string.IsNullOrEmpty(existingPage.Id) && existingPage.Id == settingsPage.Id)
        {
            return true;
        }

        if (existingPage.GetType() == settingsPage.GetType())
        {
            return true;
        }

        return false;
    }

    private static void AppendContextItemGroup(List<IContextItem?> contextItems, List<IContextItem> group, bool hasPrimaryCommand)
    {
        if (group.Count == 0)
        {
            return;
        }

        var hasExistingMenuItems = hasPrimaryCommand || contextItems.Count > 0;
        if (hasExistingMenuItems &&
            (contextItems.Count == 0 ||
             contextItems[^1] is not ISeparatorContextItem))
        {
            contextItems.Add(new Separator());
        }

        contextItems.AddRange(group);
    }

    private void TryAddPinToDockCommand(
        ProviderSettings providerSettings,
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

    private abstract partial class OpenExtensionSettingsCommand : InvokableCommand
    {
        private readonly CommandProviderWrapper _provider;
        private readonly SettingsModel _settingsModel;
        private readonly string? _commandId;

        protected abstract string NameResource { get; }

        protected abstract ExtensionSettingsFocusTarget FocusTarget { get; }

        public override string Name => RS_.GetString(NameResource);

        protected OpenExtensionSettingsCommand(CommandProviderWrapper provider, SettingsModel settingsModel, string? commandId)
        {
            _provider = provider;
            _settingsModel = settingsModel;
            _commandId = commandId;
        }

        public override CommandResult Invoke()
        {
            var providerSettings = _settingsModel.GetProviderSettings(_provider);
            var providerSettingsViewModel = new ProviderSettingsViewModel(_provider, providerSettings, _settingsModel);
            var extensionSettingsRequest = new ExtensionSettingsNavigationRequest(providerSettingsViewModel, _commandId, FocusTarget);
            WeakReferenceMessenger.Default.Send(new OpenSettingsMessage(ExtensionSettingsRequest: extensionSettingsRequest));
            return CommandResult.KeepOpen();
        }
    }

    private sealed partial class OpenExtensionSettingsPageCommand : OpenExtensionSettingsCommand
    {
        protected override string NameResource => "open_extension_settings_command_name";

        protected override ExtensionSettingsFocusTarget FocusTarget => ExtensionSettingsFocusTarget.SettingsPage;

        public override IconInfo Icon => Icons.SettingsIcon;

        public OpenExtensionSettingsPageCommand(CommandProviderWrapper provider, SettingsModel settingsModel)
            : base(provider, settingsModel, commandId: null)
        {
        }
    }

    private sealed partial class OpenCommandAliasSettingsCommand : OpenExtensionSettingsCommand
    {
        protected override string NameResource => "change_alias_command_name";

        protected override ExtensionSettingsFocusTarget FocusTarget => ExtensionSettingsFocusTarget.Alias;

        public override IconInfo Icon => Icons.AliasIcon;

        public OpenCommandAliasSettingsCommand(CommandProviderWrapper provider, SettingsModel settingsModel, string commandId)
            : base(provider, settingsModel, commandId)
        {
        }
    }

    private sealed partial class OpenCommandGlobalHotkeySettingsCommand : OpenExtensionSettingsCommand
    {
        protected override string NameResource => "change_global_hotkey_command_name";

        protected override ExtensionSettingsFocusTarget FocusTarget => ExtensionSettingsFocusTarget.GlobalHotkey;

        public override IconInfo Icon => Icons.GlobalHotkeyIcon;

        public OpenCommandGlobalHotkeySettingsCommand(CommandProviderWrapper provider, SettingsModel settingsModel, string commandId)
            : base(provider, settingsModel, commandId)
        {
        }
    }
}
