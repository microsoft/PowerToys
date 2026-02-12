// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
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

        // Don't add pin to dock option if the dock isn't enabled
        if (!_settingsModel.EnableDock)
        {
            return results;
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

                var inStartBands = _settingsModel.DockSettings.StartBands.Any(band => band.Id == this.Id);
                var inCenterBands = _settingsModel.DockSettings.CenterBands.Any(band => band.Id == this.Id);
                var inEndBands = _settingsModel.DockSettings.EndBands.Any(band => band.Id == this.Id);
                var alreadyPinned = (inStartBands || inCenterBands || inEndBands) &&
                                    _settingsModel.DockSettings.PinnedCommands.Contains(this.Id);

                var pinCommand = new PinToDockItem(commandItem, !alreadyPinned, _topLevelCommandManager, _settingsModel);
                var pinCommandViewModel = new CommandContextItemViewModel(pinCommand, commandItem.PageContext);
                pinCommandViewModel.SlowInitializeProperties();
                results.Add(pinCommandViewModel);
            }
        }

        return results;
    }

    private sealed partial class PinToDockItem : CommandContextItem
    {
        private static readonly PinToDockCommand PinCommand = new();
        private readonly CommandItemViewModel _owner;
        private readonly SettingsModel _settings;
        private readonly TopLevelCommandManager _topLevelCommandManager;
        private readonly bool _pin;

        internal CommandItemViewModel Owner => _owner;

        // public override IconInfo Icon => _pin ? Icons.PinIcon : Icons.UnpinIcon;

        // public override string Title => _pin ? Properties.Resources.dock_pin_command_name : Properties.Resources.dock_unpin_command_name;
        private string Id => _owner.Command.Id;

        public PinToDockItem(
            CommandItemViewModel owner,
            bool pin,
            TopLevelCommandManager topLevelCommandManager,
            SettingsModel settingsModel)
            : base(PinCommand)
        {
            _pin = pin;
            _owner = owner;
            _topLevelCommandManager = topLevelCommandManager;
            _settings = settingsModel;
        }

        public CommandResult Invoke()
        {
            CoreLogger.LogDebug($"PinToDockItem.Invoke({_pin}): {Id}");

            if (_pin)
            {
                PinToDock();
            }
            else
            {
                UnpinFromDock();
            }

            // // Notify that the MoreCommands have changed, so the context menu updates
            // _topLevelViewModel.PropChanged?.Invoke(
            //     _topLevelViewModel,
            //     new PropChangedEventArgs(nameof(ICommandItem.MoreCommands)));

            // TODO! what's the least gross way to cause the
            // CommandItemViewModel to re-fetch it's context menu?
            return CommandResult.GoHome();
        }

        private void PinToDock()
        {
            // It's possible that the top-level command shares an ID with a
            // band. In that case, we don't want to add it to PinnedCommands.
            // PinnedCommands is just for top-level commands IDs that aren't
            // otherwise bands.
            //
            // Check the top-level command ID against the bands first.
            if (_topLevelCommandManager.DockBands.Any(band => band.Id == Id))
            {
            }
            else
            {
                // In this case, the ID isn't another band, so add it to
                // PinnedCommands.
                //
                // TODO!! We need to include the extension ID in the pinned
                // command somehow, so that we know where to look this command
                // up later.
                if (!_settings.DockSettings.PinnedCommands.Contains(Id))
                {
                    _settings.DockSettings.PinnedCommands.Add(Id);
                }
            }

            // TODO! Deal with "the command ID is already pinned in
            // PinnedCommands but not in one of StartBands/EndBands". I think
            // we're already avoiding adding it to PinnedCommands above, but I
            // think that PinDockBand below will create a duplicate VM for it.
            _settings.DockSettings.StartBands.Add(new DockBandSettings()
            {
                Id = Id,
                ShowLabels = true,
            });

            // Create a new band VM from our current TLVM. This will allow us to
            // update the bands in the CommandProviderWrapper and the TLCM,
            // without forcing a whole reload
            var bandVm = _topLevelViewModel.CloneAsBand();
            _topLevelCommandManager.PinDockBand(bandVm);

            // _topLevelViewModel.Save();
            SettingsModel.SaveSettings(_settings);
        }

        private void UnpinFromDock()
        {
            _settings.DockSettings.PinnedCommands.Remove(Id);
            _settings.DockSettings.StartBands.RemoveAll(band => band.Id == Id);
            _settings.DockSettings.CenterBands.RemoveAll(band => band.Id == Id);
            _settings.DockSettings.EndBands.RemoveAll(band => band.Id == Id);

            // _topLevelViewModel.Save();
            SettingsModel.SaveSettings(_settings);
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
            if (sender is PinToDockItem pinItem)
            {
                pinItem.Invoke();
                return CommandResult.ShowToast($"Attempted to toggle pin to dock for {pinItem.Owner.Title}");
            }

            return CommandResult.ShowToast($"Failed to get sender for command");
        }
    }
}
