// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using ManagedCommon;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.UI.ViewModels.Dock;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.DependencyInjection;
using Windows.Foundation;
using WyHash;

namespace Microsoft.CmdPal.UI.ViewModels;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public sealed partial class TopLevelViewModel : ObservableObject, IListItem, IExtendedAttributesProvider, IPrecomputedListItem
{
    private readonly SettingsModel _settings;
    private readonly ProviderSettings _providerSettings;
    private readonly IServiceProvider _serviceProvider;
    private readonly CommandItemViewModel _commandItemViewModel;
    private readonly DockViewModel? _dockViewModel;

    public CommandProviderContext ProviderContext { get; private set; }

    private string IdFromModel => IsFallback && !string.IsNullOrWhiteSpace(_fallbackId) ? _fallbackId : _commandItemViewModel.Command.Id;

    private string _fallbackId = string.Empty;

    private string _generatedId = string.Empty;

    private HotkeySettings? _hotkey;
    private IIconInfo? _initialIcon;

    private FuzzyTargetCache _titleCache;
    private FuzzyTargetCache _subtitleCache;
    private FuzzyTargetCache _extensionNameCache;

    private CommandAlias? Alias { get; set; }

    public bool IsFallback { get; private set; }

    [ObservableProperty]
    public partial ObservableCollection<Tag> Tags { get; set; } = [];

    public string Id => string.IsNullOrWhiteSpace(IdFromModel) ? _generatedId : IdFromModel;

    public CommandPaletteHost ExtensionHost { get; private set; }

    public string ExtensionName => ExtensionHost.GetExtensionDisplayName() ?? string.Empty;

    public CommandViewModel CommandViewModel => _commandItemViewModel.Command;

    public CommandItemViewModel ItemViewModel => _commandItemViewModel;

    public string CommandProviderId => ProviderContext.ProviderId;

    public IconInfoViewModel IconViewModel => _commandItemViewModel.Icon;

    ////// ICommandItem
    public string Title => _commandItemViewModel.Title;

    public string Subtitle => _commandItemViewModel.Subtitle;

    public IIconInfo Icon => (IIconInfo)IconViewModel;

    public IIconInfo InitialIcon => _initialIcon ?? _commandItemViewModel.Icon;

    ICommand? ICommandItem.Command => _commandItemViewModel.Command.Model.Unsafe;

    IContextItem?[] ICommandItem.MoreCommands => BuildContextMenu();

    ////// IListItem
    ITag[] IListItem.Tags => Tags.ToArray();

    IDetails? IListItem.Details => null;

    string IListItem.Section => string.Empty;

    string IListItem.TextToSuggest => string.Empty;

    ////// INotifyPropChanged
    public event TypedEventHandler<object, IPropChangedEventArgs>? PropChanged;

    // Fallback items
    public string DisplayTitle { get; private set; } = string.Empty;

    public HotkeySettings? Hotkey
    {
        get => _hotkey;
        set
        {
            _serviceProvider.GetService<HotkeyManager>()!.UpdateHotkey(Id, value);
            UpdateHotkey();
            UpdateTags();
            Save();
        }
    }

    public bool HasAlias => !string.IsNullOrEmpty(AliasText);

    public string AliasText
    {
        get => Alias?.Alias ?? string.Empty;
        set
        {
            var previousAlias = Alias?.Alias ?? string.Empty;

            if (string.IsNullOrEmpty(value))
            {
                Alias = null;
            }
            else
            {
                if (Alias is CommandAlias a)
                {
                    a.Alias = value;
                }
                else
                {
                    Alias = new CommandAlias(value, Id);
                }
            }

            // Only call HandleChangeAlias if there was an actual change.
            if (previousAlias != Alias?.Alias)
            {
                HandleChangeAlias();
                OnPropertyChanged(nameof(AliasText));
                OnPropertyChanged(nameof(IsDirectAlias));
            }
        }
    }

    public bool IsDirectAlias
    {
        get => Alias?.IsDirect ?? false;
        set
        {
            if (Alias is CommandAlias a)
            {
                a.IsDirect = value;
            }

            HandleChangeAlias();
            OnPropertyChanged(nameof(IsDirectAlias));
        }
    }

    public bool IsEnabled
    {
        get
        {
            if (IsFallback)
            {
                if (_providerSettings.FallbackCommands.TryGetValue(_fallbackId, out var fallbackSettings))
                {
                    return fallbackSettings.IsEnabled;
                }

                return true;
            }
            else
            {
                return _providerSettings.IsEnabled;
            }
        }
    }

    // Dock properties
    public bool IsDockBand { get; private set; }

    public DockBandSettings? DockBandSettings
    {
        get
        {
            if (!IsDockBand)
            {
                return null;
            }

            var bandSettings = _settings.DockSettings.StartBands
                .Concat(_settings.DockSettings.EndBands)
                .FirstOrDefault(band => band.Id == this.Id);
            if (bandSettings is null)
            {
                return new DockBandSettings()
                {
                    Id = this.Id,
                    ShowLabels = true,
                };
            }

            return bandSettings;
        }
    }

    public TopLevelViewModel(
        CommandItemViewModel item,
        TopLevelType topLevelType,
        CommandPaletteHost extensionHost,
        CommandProviderContext commandProviderContext,
        SettingsModel settings,
        ProviderSettings providerSettings,
        IServiceProvider serviceProvider,
        ICommandItem? commandItem)
    {
        _serviceProvider = serviceProvider;
        _settings = settings;
        _providerSettings = providerSettings;
        ProviderContext = commandProviderContext;
        _commandItemViewModel = item;

        IsFallback = topLevelType == TopLevelType.Fallback;
        IsDockBand = topLevelType == TopLevelType.DockBand;
        ExtensionHost = extensionHost;
        if (IsFallback && commandItem is FallbackCommandItem fallback)
        {
            _fallbackId = fallback.Id;
        }

        item.PropertyChangedBackground += Item_PropertyChanged;

        _dockViewModel = serviceProvider.GetService<DockViewModel>();
    }

    internal void InitializeProperties()
    {
        ItemViewModel.SlowInitializeProperties();
        GenerateId();

        if (IsFallback)
        {
            var model = _commandItemViewModel.Model.Unsafe;

            // RPC to check type
            if (model is IFallbackCommandItem fallback)
            {
                DisplayTitle = fallback.DisplayTitle;
            }

            UpdateInitialIcon(false);
        }
    }

    private void Item_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.PropertyName))
        {
            PropChanged?.Invoke(this, new PropChangedEventArgs(e.PropertyName));

            if (e.PropertyName is nameof(CommandItemViewModel.Title) or nameof(CommandItemViewModel.Name))
            {
                _titleCache.Invalidate();
            }
            else if (e.PropertyName is nameof(CommandItemViewModel.Subtitle))
            {
                _subtitleCache.Invalidate();
            }

            if (e.PropertyName is "IsInitialized" or nameof(CommandItemViewModel.Command))
            {
                GenerateId();

                FetchAliasFromAliasManager();
                UpdateHotkey();
                UpdateTags();
                UpdateInitialIcon();
            }
            else if (e.PropertyName == nameof(CommandItem.Icon))
            {
                UpdateInitialIcon();
            }
            else if (e.PropertyName == nameof(CommandItem.DataPackage))
            {
                DoOnUiThread(() =>
                {
                    OnPropertyChanged(nameof(CommandItem.DataPackage));
                });
            }
        }
    }

    private void UpdateInitialIcon(bool raiseNotification = true)
    {
        if (_initialIcon != null || !_commandItemViewModel.Icon.IsSet)
        {
            return;
        }

        _initialIcon = (IIconInfo?)_commandItemViewModel.Icon;

        if (raiseNotification)
        {
            DoOnUiThread(
                () =>
                {
                    PropChanged?.Invoke(this, new PropChangedEventArgs(nameof(InitialIcon)));
                });
        }
    }

    private void Save() => SettingsModel.SaveSettings(_settings);

    private void HandleChangeAlias()
    {
        SetAlias();
        Save();
    }

    public void SetAlias()
    {
        var commandAlias = Alias is null
                ? null
                : new CommandAlias(Alias.Alias, Alias.CommandId, Alias.IsDirect);

        _serviceProvider.GetService<AliasManager>()!.UpdateAlias(Id, commandAlias);
        UpdateTags();
    }

    private void FetchAliasFromAliasManager()
    {
        var am = _serviceProvider.GetService<AliasManager>();
        if (am is not null)
        {
            var commandAlias = am.AliasFromId(Id);
            if (commandAlias is not null)
            {
                // Decouple from the alias manager alias object
                Alias = new CommandAlias(commandAlias.Alias, commandAlias.CommandId, commandAlias.IsDirect);
            }
        }
    }

    private void UpdateHotkey()
    {
        var hotkey = _settings.CommandHotkeys.Where(hk => hk.CommandId == Id).FirstOrDefault();
        if (hotkey is not null)
        {
            _hotkey = hotkey.Hotkey;
        }
    }

    private void UpdateTags()
    {
        List<Tag> tags = [];

        if (Hotkey is not null)
        {
            tags.Add(new Tag() { Text = Hotkey.ToString() });
        }

        if (Alias is not null)
        {
            tags.Add(new Tag() { Text = Alias.SearchPrefix });
        }

        DoOnUiThread(
            () =>
            {
                ListHelpers.InPlaceUpdateList(Tags, tags);
                PropChanged?.Invoke(this, new PropChangedEventArgs(nameof(Tags)));
            });
    }

    private void GenerateId()
    {
        // Use WyHash64 to generate stable ID hashes.
        // manually seeding with 0, so that the hash is stable across launches
        var result = WyHash64.ComputeHash64(CommandProviderId + DisplayTitle + Title + Subtitle, seed: 0);
        _generatedId = $"{CommandProviderId}{result}";
    }

    private void DoOnUiThread(Action action)
    {
        if (_commandItemViewModel.PageContext.TryGetTarget(out var pageContext))
        {
            Task.Factory.StartNew(
                action,
                CancellationToken.None,
                TaskCreationOptions.None,
                pageContext.Scheduler);
        }
    }

    internal bool SafeUpdateFallbackTextSynchronous(string newQuery)
    {
        if (!IsFallback)
        {
            return false;
        }

        if (!IsEnabled)
        {
            return false;
        }

        try
        {
            return UnsafeUpdateFallbackSynchronous(newQuery);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex.ToString());
        }

        return false;
    }

    /// <summary>
    /// Calls UpdateQuery on our command, if we're a fallback item. This does
    /// RPC work, so make sure you're calling it on a BG thread.
    /// </summary>
    /// <param name="newQuery">The new search text to pass to the extension</param>
    /// <returns>true if our Title changed across this call</returns>
    private bool UnsafeUpdateFallbackSynchronous(string newQuery)
    {
        var model = _commandItemViewModel.Model.Unsafe;

        // RPC to check type
        if (model is IFallbackCommandItem fallback)
        {
            var wasEmpty = string.IsNullOrEmpty(Title);

            // RPC for method
            fallback.FallbackHandler.UpdateQuery(newQuery);
            var isEmpty = string.IsNullOrEmpty(Title);
            return wasEmpty != isEmpty;
        }

        return false;
    }

    public PerformCommandMessage GetPerformCommandMessage()
    {
        return new PerformCommandMessage(this.CommandViewModel.Model, new Models.ExtensionObject<IListItem>(this));
    }

    public override string ToString()
    {
        return $"{nameof(TopLevelViewModel)}: {Id} ({Title}) - display: {DisplayTitle} - fallback: {IsFallback} - enabled: {IsEnabled}";
    }

    public IDictionary<string, object?> GetProperties()
    {
        return new Dictionary<string, object?>
        {
            [WellKnownExtensionAttributes.DataPackage] = _commandItemViewModel?.DataPackage,
        };
    }

    public FuzzyTarget GetTitleTarget(IPrecomputedFuzzyMatcher matcher)
        => _titleCache.GetOrUpdate(matcher, Title);

    public FuzzyTarget GetSubtitleTarget(IPrecomputedFuzzyMatcher matcher)
        => _subtitleCache.GetOrUpdate(matcher, Subtitle);

    public FuzzyTarget GetExtensionNameTarget(IPrecomputedFuzzyMatcher matcher)
        => _extensionNameCache.GetOrUpdate(matcher, ExtensionName);

    private string GetDebuggerDisplay()
    {
        return ToString();
    }

    private IContextItem?[] BuildContextMenu()
    {
        List<IContextItem?> contextItems = new();

        foreach (var item in _commandItemViewModel.MoreCommands)
        {
            if (item is ISeparatorContextItem)
            {
                contextItems.Add(item as IContextItem);
            }
            else if (item is CommandContextItemViewModel commandItem)
            {
                contextItems.Add(commandItem.Model.Unsafe);
            }
        }

        var dockEnabled = _settings.EnableDock;
        if (dockEnabled && _dockViewModel is not null)
        {
            // Add a separator
            contextItems.Add(new Separator());

            var inStartBands = _settings.DockSettings.StartBands.Any(band => band.Id == this.Id);
            var inCenterBands = _settings.DockSettings.CenterBands.Any(band => band.Id == this.Id);
            var inEndBands = _settings.DockSettings.EndBands.Any(band => band.Id == this.Id);
            var alreadyPinned = (inStartBands || inCenterBands || inEndBands) &&
                                _settings.DockSettings.PinnedCommands.Contains(this.Id);

            var pinCommand = new PinToDockCommand(
                this,
                !alreadyPinned,
                _dockViewModel,
                _settings,
                _serviceProvider.GetService<TopLevelCommandManager>()!);

            var contextItem = new CommandContextItem(pinCommand);

            contextItems.Add(contextItem);
        }

        return contextItems.ToArray();
    }

    internal ICommandItem ToPinnedDockBandItem()
    {
        var item = new PinnedDockItem(item: this, id: Id);

        return item;
    }

    internal TopLevelViewModel CloneAsBand()
    {
        return new TopLevelViewModel(
            _commandItemViewModel,
            TopLevelType.DockBand,
            ExtensionHost,
            ProviderContext,
            _settings,
            _providerSettings,
            _serviceProvider,
            _commandItemViewModel.Model.Unsafe);
    }

    private sealed partial class PinToDockCommand : InvokableCommand
    {
        private readonly TopLevelViewModel _topLevelViewModel;
        private readonly DockViewModel _dockViewModel;
        private readonly SettingsModel _settings;
        private readonly TopLevelCommandManager _topLevelCommandManager;
        private readonly bool _pin;

        public override IconInfo Icon => _pin ? Icons.PinIcon : Icons.UnpinIcon;

        public override string Name => _pin ? Properties.Resources.dock_pin_command_name : Properties.Resources.dock_unpin_command_name;

        public PinToDockCommand(
            TopLevelViewModel topLevelViewModel,
            bool pin,
            DockViewModel dockViewModel,
            SettingsModel settings,
            TopLevelCommandManager topLevelCommandManager)
        {
            _topLevelViewModel = topLevelViewModel;
            _dockViewModel = dockViewModel;
            _settings = settings;
            _topLevelCommandManager = topLevelCommandManager;
            _pin = pin;
        }

        public override CommandResult Invoke()
        {
            Logger.LogDebug($"PinToDockCommand.Invoke({_pin}): {_topLevelViewModel.Id}");
            if (_pin)
            {
                PinToDock();
            }
            else
            {
                UnpinFromDock();
            }

            // Notify that the MoreCommands have changed, so the context menu updates
            _topLevelViewModel.PropChanged?.Invoke(
                _topLevelViewModel,
                new PropChangedEventArgs(nameof(ICommandItem.MoreCommands)));
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
            if (_topLevelCommandManager.DockBands.Any(band => band.Id == _topLevelViewModel.Id))
            {
            }
            else
            {
                // In this case, the ID isn't another band, so add it to PinnedCommands.
                if (!_settings.DockSettings.PinnedCommands.Contains(_topLevelViewModel.Id))
                {
                    _settings.DockSettings.PinnedCommands.Add(_topLevelViewModel.Id);
                }
            }

            // TODO! Deal with "the command ID is already pinned in
            // PinnedCommands but not in one of StartBands/EndBands". I think
            // we're already avoiding adding it to PinnedCommands above, but I
            // think that PinDockBand below will create a duplicate VM for it.
            _settings.DockSettings.StartBands.Add(new DockBandSettings()
            {
                Id = _topLevelViewModel.Id,
                ShowLabels = true,
            });

            // Create a new band VM from our current TopLevelViewModel. This
            // will allow us to update the bands in the CommandProviderWrapper
            // and the TopLevelCommandManager, without forcing a whole reload
            var bandVm = _topLevelViewModel.CloneAsBand();
            _topLevelCommandManager.PinDockBand(bandVm);

            _topLevelViewModel.Save();
        }

        private void UnpinFromDock()
        {
            _settings.DockSettings.PinnedCommands.Remove(_topLevelViewModel.Id);
            _settings.DockSettings.StartBands.RemoveAll(band => band.Id == _topLevelViewModel.Id);
            _settings.DockSettings.CenterBands.RemoveAll(band => band.Id == _topLevelViewModel.Id);
            _settings.DockSettings.EndBands.RemoveAll(band => band.Id == _topLevelViewModel.Id);

            _topLevelViewModel.Save();
        }
    }
}

public enum TopLevelType
{
    Normal,
    Fallback,
    DockBand,
}
