// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using ManagedCommon;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.Common.Text;
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
    private readonly IContextMenuFactory _contextMenuFactory;

    public ICommandProviderContext ProviderContext { get; private set; }

    private string IdFromModel => IsFallback && !string.IsNullOrWhiteSpace(_fallbackId) ? _fallbackId : _commandItemViewModel.Command.Id;

    private string _fallbackId = string.Empty;
    private string _generatedId = string.Empty;

    private HotkeySettings? _hotkey;
    private IIconInfo? _initialIcon;

    private FuzzyTargetCache _titleCache;
    private FuzzyTargetCache _subtitleCache;
    private FuzzyTargetCache _extensionNameCache;
    private FallbackExecutionState? _fallbackExecutionState;
    private IFallbackCommandItemDefaults? _fallbackCommandItemDefaults;
    private FallbackSnapshotItemCache? _fallbackSnapshotItemCache;
    private bool _usesAsyncFallbackEvaluation;
    private HostMatchKind? _fallbackHostMatchKind;

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

    internal bool UsesInlineFallbackEvaluation
    {
        get
        {
            CacheFallbackInterfaces(_commandItemViewModel.Model.Unsafe);
            return _fallbackExecutionState?.UsesInlineEvaluation ?? false;
        }
    }

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
                .Concat(_settings.DockSettings.CenterBands)
                .Concat(_settings.DockSettings.EndBands)
                .FirstOrDefault(band => band.CommandId == this.Id);
            if (bandSettings is null)
            {
                return new DockBandSettings()
                {
                    ProviderId = this.CommandProviderId,
                    CommandId = this.Id,
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
        ICommandProviderContext commandProviderContext,
        SettingsModel settings,
        ProviderSettings providerSettings,
        IServiceProvider serviceProvider,
        ICommandItem? commandItem,
        IContextMenuFactory? contextMenuFactory)
    {
        _serviceProvider = serviceProvider;
        _settings = settings;
        _providerSettings = providerSettings;
        ProviderContext = commandProviderContext;
        _commandItemViewModel = item;

        _contextMenuFactory = contextMenuFactory ?? DefaultContextMenuFactory.Instance;

        IsFallback = topLevelType == TopLevelType.Fallback;
        IsDockBand = topLevelType == TopLevelType.DockBand;
        ExtensionHost = extensionHost;
        if (IsFallback && commandItem is FallbackCommandItem fallback)
        {
            _fallbackId = fallback.Id;
        }

        item.PropertyChangedBackground += Item_PropertyChanged;
    }

    internal void InitializeProperties()
    {
        // Init first, so that we get the ID & titles,
        // then generate the ID,
        // then slow init for the context menu
        ItemViewModel.InitializeProperties();
        GenerateId();
        ItemViewModel.SlowInitializeProperties();

        if (IsFallback)
        {
            var model = _commandItemViewModel.Model.Unsafe;
            CacheFallbackInterfaces(model);

            UpdateInitialIcon(false);
        }
    }

    private void CacheFallbackInterfaces(object? model)
    {
        if (!IsFallback || _fallbackExecutionState is not null || model is not IFallbackCommandItem fallback)
        {
            return;
        }

        if (model is IFallbackCommandItem2 fallback2)
        {
            _fallbackId = fallback2.Id;
        }

        var asyncFallbackHandler = fallback.FallbackHandler as IFallbackHandler2;
        var hostMatchedFallbackCommandItem = model as IHostMatchedFallbackCommandItem;

        _fallbackExecutionState = new FallbackExecutionState(
            fallback,
            asyncFallbackHandler,
            model as IFormattedFallbackCommandItem,
            hostMatchedFallbackCommandItem,
            GetFallbackExecutionPolicy,
            MaterializeSnapshotItems,
            FallbackRefreshNotifier.RequestRefresh);
        _fallbackCommandItemDefaults = model as IFallbackCommandItemDefaults;
        _fallbackSnapshotItemCache = new FallbackSnapshotItemCache(ExtensionHost, ProviderContext, Id, ExtensionName);
        _usesAsyncFallbackEvaluation = asyncFallbackHandler is not null;
        _fallbackHostMatchKind = hostMatchedFallbackCommandItem?.MatchKind;

        DisplayTitle = fallback.DisplayTitle;
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
        if (!IsFallback || !IsEnabled)
        {
            return false;
        }

        CacheFallbackInterfaces(_commandItemViewModel.Model.Unsafe);
        if (_fallbackExecutionState is null)
        {
            return false;
        }

        try
        {
            return _fallbackExecutionState.UpdateSynchronous(newQuery, this);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex.ToString());
            return false;
        }
    }

    internal bool SafeUpdateFallbackTextInline(string newQuery)
    {
        if (!IsFallback || !IsEnabled)
        {
            return false;
        }

        CacheFallbackInterfaces(_commandItemViewModel.Model.Unsafe);
        if (_fallbackExecutionState is null)
        {
            return false;
        }

        try
        {
            return _fallbackExecutionState.UpdateInline(newQuery, this);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex.ToString());
            return false;
        }
    }

    internal IListItem[] GetCurrentFallbackItems()
    {
        CacheFallbackInterfaces(_commandItemViewModel.Model.Unsafe);
        return _fallbackExecutionState?.GetCurrentItems() ?? [];
    }

    internal void CancelOutstandingFallbackQuery()
    {
        if (!IsFallback)
        {
            return;
        }

        CacheFallbackInterfaces(_commandItemViewModel.Model.Unsafe);
        _fallbackExecutionState?.CancelOutstandingQuery();
        _fallbackSnapshotItemCache?.Clear();
    }

    internal bool IsCurrentFallbackQueryId(string queryId)
    {
        CacheFallbackInterfaces(_commandItemViewModel.Model.Unsafe);
        return _fallbackExecutionState?.IsCurrentQueryId(queryId) ?? false;
    }

    internal bool UsesAsyncFallbackEvaluation
    {
        get
        {
            CacheFallbackInterfaces(_commandItemViewModel.Model.Unsafe);
            return _usesAsyncFallbackEvaluation;
        }
    }

    internal HostMatchKind? FallbackHostMatchKind
    {
        get
        {
            CacheFallbackInterfaces(_commandItemViewModel.Model.Unsafe);
            return _fallbackHostMatchKind;
        }
    }

    internal FallbackExecutionPolicy GetFallbackExecutionPolicy()
    {
        CacheFallbackInterfaces(_commandItemViewModel.Model.Unsafe);
        if (string.IsNullOrEmpty(_fallbackId))
        {
            return FallbackExecutionPolicy.Empty;
        }

        _providerSettings.FallbackCommands.TryGetValue(_fallbackId, out var fallbackSettings);
        return ProviderSettings.GetEffectiveFallbackExecutionPolicy(_fallbackId, fallbackSettings, _fallbackCommandItemDefaults);
    }

    internal uint? GetSuggestedFallbackQueryDelayMilliseconds()
    {
        CacheFallbackInterfaces(_commandItemViewModel.Model.Unsafe);
        return string.IsNullOrEmpty(_fallbackId)
            ? null
            : ProviderSettings.GetSuggestedFallbackQueryDelayMilliseconds(_fallbackId, _fallbackCommandItemDefaults);
    }

    internal uint? GetSuggestedFallbackMinQueryLength()
    {
        CacheFallbackInterfaces(_commandItemViewModel.Model.Unsafe);
        return string.IsNullOrEmpty(_fallbackId)
            ? null
            : ProviderSettings.GetSuggestedFallbackMinQueryLength(_fallbackId, _fallbackCommandItemDefaults);
    }

    private IListItem[] MaterializeSnapshotItems(string query, string queryId, IReadOnlyList<FallbackSnapshotDefinition> snapshotItems)
    {
        _fallbackSnapshotItemCache ??= new FallbackSnapshotItemCache(ExtensionHost, ProviderContext, Id, ExtensionName);

        var invocationArgs = new FallbackCommandInvocationArgs()
        {
            Query = query,
            QueryId = queryId,
        };

        return _fallbackSnapshotItemCache.Materialize(
            snapshotItems,
            AliasText,
            HasAlias,
            invocationArgs,
            () => IsCurrentFallbackQueryId(queryId));
    }

    internal static bool IsRegexMatch(string pattern, string query)
    {
        return FallbackExecutionState.IsRegexMatch(pattern, query);
    }

    public PerformCommandMessage GetPerformCommandMessage()
    {
        return new PerformCommandMessage(this.CommandViewModel.Model, new Models.ExtensionObject<IListItem>(this), GetCurrentFallbackInvocationArgs());
    }

    private IFallbackCommandInvocationArgs? GetCurrentFallbackInvocationArgs()
    {
        if (!IsFallback)
        {
            return null;
        }

        CacheFallbackInterfaces(_commandItemViewModel.Model.Unsafe);
        return _fallbackExecutionState?.GetCurrentInvocationArgs();
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

    /// <summary>
    /// Helper to convert our context menu viewmodels back into the API
    /// interfaces that ICommandItem expects.
    /// </summary>
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

        _contextMenuFactory.AddMoreCommandsToTopLevel(this, this.ProviderContext, contextItems);

        return contextItems.ToArray();
    }

    internal ICommandItem ToPinnedDockBandItem()
    {
        var item = new PinnedDockItem(item: this, id: Id);

        return item;
    }
}

public enum TopLevelType
{
    Normal,
    Fallback,
    DockBand,
}
