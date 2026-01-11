// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ManagedCommon;
using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.CmdPal.Core.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.DependencyInjection;
using Windows.Foundation;
using WyHash;

namespace Microsoft.CmdPal.UI.ViewModels;

public sealed partial class TopLevelViewModel : ObservableObject, IListItem, IExtendedAttributesProvider
{
    private readonly SettingsModel _settings;
    private readonly ProviderSettings _providerSettings;
    private readonly IServiceProvider _serviceProvider;
    private readonly CommandItemViewModel _commandItemViewModel;

    private readonly string _commandProviderId;

    private string IdFromModel => IsFallback && !string.IsNullOrWhiteSpace(_fallbackId) ? _fallbackId : _commandItemViewModel.Command.Id;

    private string _fallbackId = string.Empty;

    private string _generatedId = string.Empty;

    private HotkeySettings? _hotkey;
    private IIconInfo? _initialIcon;

    private CommandAlias? Alias { get; set; }

    public bool IsFallback { get; private set; }

    [ObservableProperty]
    public partial ObservableCollection<Tag> Tags { get; set; } = [];

    public string Id => string.IsNullOrWhiteSpace(IdFromModel) ? _generatedId : IdFromModel;

    public CommandPaletteHost ExtensionHost { get; private set; }

    public CommandViewModel CommandViewModel => _commandItemViewModel.Command;

    public CommandItemViewModel ItemViewModel => _commandItemViewModel;

    public string CommandProviderId => _commandProviderId;

    ////// ICommandItem
    public string Title => _commandItemViewModel.Title;

    public string Subtitle => _commandItemViewModel.Subtitle;

    public IIconInfo Icon => _commandItemViewModel.Icon;

    public IIconInfo InitialIcon => _initialIcon ?? _commandItemViewModel.Icon;

    ICommand? ICommandItem.Command => _commandItemViewModel.Command.Model.Unsafe;

    IContextItem?[] ICommandItem.MoreCommands => _commandItemViewModel.MoreCommands
                                                    .Select(item =>
                                                    {
                                                        if (item is ISeparatorContextItem)
                                                        {
                                                            return item as IContextItem;
                                                        }
                                                        else if (item is CommandContextItemViewModel commandItem)
                                                        {
                                                            return commandItem.Model.Unsafe;
                                                        }
                                                        else
                                                        {
                                                            return null;
                                                        }
                                                    }).ToArray();

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

    public TopLevelViewModel(
        CommandItemViewModel item,
        bool isFallback,
        CommandPaletteHost extensionHost,
        string commandProviderId,
        SettingsModel settings,
        ProviderSettings providerSettings,
        IServiceProvider serviceProvider,
        ICommandItem? commandItem)
    {
        _serviceProvider = serviceProvider;
        _settings = settings;
        _providerSettings = providerSettings;
        _commandProviderId = commandProviderId;
        _commandItemViewModel = item;

        IsFallback = isFallback;
        ExtensionHost = extensionHost;
        if (isFallback && commandItem is FallbackCommandItem fallback)
        {
            _fallbackId = fallback.Id;
        }

        item.PropertyChanged += Item_PropertyChanged;

        // UpdateAlias();
        // UpdateHotkey();
        // UpdateTags();
    }

    internal void InitializeProperties()
    {
        ItemViewModel.SlowInitializeProperties();

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

        _initialIcon = _commandItemViewModel.Icon;

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
        var result = WyHash64.ComputeHash64(_commandProviderId + DisplayTitle + Title + Subtitle, seed: 0);
        _generatedId = $"{_commandProviderId}{result}";
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
        return new PerformCommandMessage(this.CommandViewModel.Model, new Core.ViewModels.Models.ExtensionObject<IListItem>(this));
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
}
