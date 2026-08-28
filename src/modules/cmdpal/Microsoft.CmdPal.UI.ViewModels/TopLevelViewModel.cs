// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using ManagedCommon;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Services;
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
    private const int MaximumFallbackRegexLength = 4096;
    private static readonly TimeSpan FallbackRegexTimeout = TimeSpan.FromMilliseconds(50);
    private readonly ISettingsService _settingsService;
    private readonly ProviderSettings _providerSettings;
    private readonly IServiceProvider _serviceProvider;
    private readonly CommandItemViewModel _commandItemViewModel;
    private readonly IContextMenuFactory _contextMenuFactory;

    public ICommandProviderContext ProviderContext { get; private set; }

    private string IdFromModel => IsFallback && !string.IsNullOrWhiteSpace(_fallbackId) ? _fallbackId : _commandItemViewModel.Command.Id;

    private string _fallbackId = string.Empty;

    private string _generatedId = string.Empty;
    private IFallbackCommandItem3? _fallbackV2;
    private IFallbackHandler2? _fallbackQueryHandler;
    private ICommand? _fallbackPlaceholderCommand;
    private HostMatchKind _fallbackMatchKind;
    private string _fallbackMatchValue = string.Empty;
    private string _fallbackTitleTemplate = string.Empty;
    private string _fallbackSubtitleTemplate = string.Empty;
    private string? _fallbackTitle;
    private string? _fallbackSubtitle;

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
    public string Title => _fallbackTitle ?? _commandItemViewModel.Title;

    public string Subtitle => _fallbackSubtitle ?? _commandItemViewModel.Subtitle;

    public IIconInfo Icon => (IIconInfo)IconViewModel;

    public IIconInfo InitialIcon => _initialIcon ?? _commandItemViewModel.Icon;

    ICommand? ICommandItem.Command => _fallbackPlaceholderCommand ?? _commandItemViewModel.Command.Model.Unsafe;

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

    internal FallbackCommandMode FallbackMode { get; private set; } = FallbackCommandMode.Active;

    internal bool IsFallbackV2 => _fallbackV2 is not null;

    internal uint? SuggestedQueryDelayMilliseconds { get; private set; }

    internal uint? SuggestedMinQueryLength { get; private set; }

    internal IFallbackHandler2? FallbackQueryHandler => _fallbackQueryHandler;

    internal string FallbackKey => $"{CommandProviderId}\0{Id}";

    internal bool IncludeInGlobalResults => GetFallbackSettings()?.IncludeInGlobalResults == true;

    internal uint EffectiveQueryDelayMilliseconds => GetFallbackSettings()?.QueryDelayMilliseconds
        ?? SuggestedQueryDelayMilliseconds
        ?? 0;

    internal uint EffectiveMinimumQueryLength => GetFallbackSettings()?.MinimumQueryLength
        ?? SuggestedMinQueryLength
        ?? 0;

    internal uint EffectiveMaximumVisibleItemCount => Math.Clamp(
        GetFallbackSettings()?.MaximumVisibleItemCount ?? FallbackResultQueryManager.InitialRequestedItemCount,
        FallbackSettings.MinimumItemCount,
        FallbackSettings.MaximumItemCount);

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
                    Alias = a with { Alias = value };
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
                Alias = a with { IsDirect = value };
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

            var bandSettings = _settingsService.Settings.DockSettings.StartBands
                .Concat(_settingsService.Settings.DockSettings.CenterBands)
                .Concat(_settingsService.Settings.DockSettings.EndBands)
                .FirstOrDefault(band => band.CommandId == this.Id);
            if (bandSettings is null)
            {
                return new DockBandSettings()
                {
                    ProviderId = this.CommandProviderId,
                    CommandId = this.Id,
                    ShowTitles = true,
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
        ProviderSettings providerSettings,
        IServiceProvider serviceProvider,
        ICommandItem? commandItem,
        IContextMenuFactory? contextMenuFactory)
    {
        _serviceProvider = serviceProvider;
        _settingsService = serviceProvider.GetRequiredService<ISettingsService>();
        _providerSettings = providerSettings;
        ProviderContext = commandProviderContext;
        _commandItemViewModel = item;

        _contextMenuFactory = contextMenuFactory ?? DefaultContextMenuFactory.Instance;

        IsFallback = topLevelType == TopLevelType.Fallback;
        IsDockBand = topLevelType == TopLevelType.DockBand;
        ExtensionHost = extensionHost;
        if (IsFallback && commandItem is IFallbackCommandItem2 fallback)
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

            // RPC to check type
            if (model is IFallbackCommandItem fallback)
            {
                DisplayTitle = fallback.DisplayTitle;
            }

            if (model is IFallbackCommandItem3 fallbackV2)
            {
                _fallbackV2 = fallbackV2;
                FallbackMode = fallbackV2.Mode;
                _fallbackMatchKind = fallbackV2.MatchKind;
                _fallbackMatchValue = fallbackV2.MatchValue;
                _fallbackTitleTemplate = fallbackV2.TitleTemplate;
                _fallbackSubtitleTemplate = fallbackV2.SubtitleTemplate;
                if (FallbackMode != FallbackCommandMode.Results)
                {
                    _fallbackPlaceholderCommand = new NoOpCommand
                    {
                        Id = $"{fallbackV2.Id}.placeholder",
                        Name = fallbackV2.Name,
                    };
                }

                SuggestedQueryDelayMilliseconds = fallbackV2.SuggestedQueryDelayMilliseconds.ToNullableUInt32();
                SuggestedMinQueryLength = fallbackV2.SuggestedMinQueryLength.ToNullableUInt32();
                if (FallbackMode == FallbackCommandMode.Results)
                {
                    _fallbackQueryHandler = fallbackV2.QueryHandler;
                }
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

    private void Save() => _settingsService.Save();

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
        var hotkey = _settingsService.Settings.CommandHotkeys.Where(hk => hk.CommandId == Id).FirstOrDefault();
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

    internal bool SafeUpdateFallbackTextSynchronous(string newQuery, string queryId, CancellationToken queryToken)
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
            return UnsafeUpdateFallbackSynchronous(newQuery, queryId, queryToken);
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
    private bool UnsafeUpdateFallbackSynchronous(string newQuery, string queryId, CancellationToken queryToken)
    {
        var model = _commandItemViewModel.Model.Unsafe;

        // RPC to check type
        if (model is IFallbackCommandItem fallback)
        {
            var oldTitle = Title;

            // RPC for method
            fallback.FallbackHandler.UpdateQuery(newQuery);
            if (queryToken.IsCancellationRequested)
            {
                return false;
            }

            SetFallbackQuery(newQuery, queryId, queryToken);
            var newTitle = Title;

            // Report any title change, not just an empty <-> non-empty flip: the render path
            // re-scores fallbacks off this signal, so a change like "server01" -> "server02"
            // must still trigger a refresh or the fallback keeps its stale score and position.
            return !string.Equals(oldTitle, newTitle, StringComparison.Ordinal);
        }

        return false;
    }

    internal bool PreparePassiveFallback(string query, string queryId, CancellationToken queryToken)
    {
        var fallback = _fallbackV2;
        if (fallback is null || FallbackMode != FallbackCommandMode.Passive || !IsEnabled)
        {
            return false;
        }

        var matches = _fallbackMatchKind != HostMatchKind.Regex || MatchesFallbackRegex(query, _fallbackMatchValue);
        var title = matches ? FormatFallbackText(_fallbackTitleTemplate, _commandItemViewModel.Title, query) : string.Empty;
        var subtitle = matches ? FormatFallbackText(_fallbackSubtitleTemplate, _commandItemViewModel.Subtitle, query) : string.Empty;
        var changed = !string.Equals(_fallbackTitle, title, StringComparison.Ordinal)
            || !string.Equals(_fallbackSubtitle, subtitle, StringComparison.Ordinal);

        _fallbackTitle = title;
        _fallbackSubtitle = subtitle;
        SetFallbackQuery(query, queryId, queryToken);

        if (changed)
        {
            _titleCache.Invalidate();
            _subtitleCache.Invalidate();
            PropChanged?.Invoke(this, new PropChangedEventArgs(nameof(Title)));
            PropChanged?.Invoke(this, new PropChangedEventArgs(nameof(Subtitle)));
        }

        return matches;
    }

    private static string FormatFallbackText(string template, string defaultValue, string query)
    {
        return string.IsNullOrEmpty(template)
            ? defaultValue
            : template.Replace("{query}", query, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests the query against a match pattern that an extension supplied.
    /// </summary>
    /// <remarks>
    /// The pattern is untrusted input, so this method limits what it can cost. The
    /// length check stops very large patterns, the timeout stops patterns that
    /// backtrack, and the anchors make the pattern match the whole query. Without the
    /// anchors an extension could claim every query with one dot. A pattern that is bad
    /// in any of these ways matches nothing instead of failing the search.
    /// </remarks>
    private static bool MatchesFallbackRegex(string query, string pattern)
    {
        if (string.IsNullOrEmpty(pattern) || pattern.Length > MaximumFallbackRegexLength)
        {
            return false;
        }

        try
        {
            return Regex.IsMatch(query, $"\\A(?:{pattern})\\z", RegexOptions.CultureInvariant, FallbackRegexTimeout);
        }
        catch (ArgumentException)
        {
            // The extension supplied a pattern that does not parse.
            return false;
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private void SetFallbackQuery(string query, string queryId, CancellationToken queryToken)
    {
        if (_fallbackV2 is null || FallbackMode == FallbackCommandMode.Results)
        {
            return;
        }

        _fallbackPlaceholderCommand = new FallbackInvocationCommand(
            _fallbackV2,
            this,
            new FallbackCommandInvocationArgs(
                query,
                queryId,
                global::Windows.System.UserProfile.GlobalizationPreferences.Languages.ToArray()),
            queryToken);
        PropChanged?.Invoke(this, new PropChangedEventArgs(nameof(ICommandItem.Command)));
    }

    private FallbackSettings? GetFallbackSettings()
    {
        return _providerSettings.FallbackCommands.TryGetValue(Id, out var settings) ? settings : null;
    }

    public PerformCommandMessage GetPerformCommandMessage()
    {
        var command = _fallbackPlaceholderCommand is null
            ? CommandViewModel.Model
            : new Models.ExtensionObject<ICommand>(_fallbackPlaceholderCommand);
        return new PerformCommandMessage(command, new Models.ExtensionObject<IListItem>(this));
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

    /// <summary>
    /// Unsubscribes from the underlying <see cref="CommandItemViewModel"/> event
    /// and cleans up its resources so the TopLevelViewModel can be garbage
    /// collected after it is removed from the owning collections.
    /// </summary>
    internal void Cleanup()
    {
        _commandItemViewModel.PropertyChangedBackground -= Item_PropertyChanged;
        _commandItemViewModel.SafeCleanup();
        _initialIcon = null;
    }
}

public enum TopLevelType
{
    Normal,
    Fallback,
    DockBand,
}
