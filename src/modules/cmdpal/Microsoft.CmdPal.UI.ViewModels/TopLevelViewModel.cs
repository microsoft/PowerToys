// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.DependencyInjection;
using Windows.Foundation;
using WyHash;

namespace Microsoft.CmdPal.UI.ViewModels;

public sealed partial class TopLevelViewModel : ObservableObject, IListItem
{
    private readonly SettingsModel _settings;
    private readonly IServiceProvider _serviceProvider;
    private readonly CommandItemViewModel _commandItemViewModel;

    private readonly string _commandProviderId;

    private string IdFromModel => _commandItemViewModel.Command.Id;

    private string _generatedId = string.Empty;

    private HotkeySettings? _hotkey;

    private CommandAlias? Alias { get; set; }

    public bool IsFallback { get; private set; }

    [ObservableProperty]
    public partial ObservableCollection<Tag> Tags { get; set; } = [];

    public string Id => string.IsNullOrEmpty(IdFromModel) ? _generatedId : IdFromModel;

    public CommandPaletteHost ExtensionHost { get; private set; }

    public CommandViewModel CommandViewModel => _commandItemViewModel.Command;

    public CommandItemViewModel ItemViewModel => _commandItemViewModel;

    ////// ICommandItem
    public string Title => _commandItemViewModel.Title;

    public string Subtitle => _commandItemViewModel.Subtitle;

    public IIconInfo Icon => _commandItemViewModel.Icon;

    ICommand? ICommandItem.Command => _commandItemViewModel.Command.Model.Unsafe;

    IContextItem?[] ICommandItem.MoreCommands => _commandItemViewModel.MoreCommands.Select(i => i.Model.Unsafe).ToArray();

    ////// IListItem
    ITag[] IListItem.Tags => Tags.ToArray();

    IDetails? IListItem.Details => null;

    string IListItem.Section => string.Empty;

    string IListItem.TextToSuggest => string.Empty;

    ////// INotifyPropChanged
    public event TypedEventHandler<object, IPropChangedEventArgs>? PropChanged;

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

            HandleChangeAlias();
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
        }
    }

    public TopLevelViewModel(
        CommandItemViewModel item,
        bool isFallback,
        CommandPaletteHost extensionHost,
        string commandProviderId,
        SettingsModel settings,
        IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _settings = settings;
        _commandProviderId = commandProviderId;
        _commandItemViewModel = item;

        IsFallback = isFallback;
        ExtensionHost = extensionHost;

        item.PropertyChanged += Item_PropertyChanged;

        // UpdateAlias();
        // UpdateHotkey();
        // UpdateTags();
    }

    private void Item_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.PropertyName))
        {
            PropChanged?.Invoke(this, new PropChangedEventArgs(e.PropertyName));

            if (e.PropertyName == "IsInitialized")
            {
                GenerateId();

                UpdateAlias();
                UpdateHotkey();
                UpdateTags();
            }
        }
    }

    private void Save() => SettingsModel.SaveSettings(_settings);

    private void HandleChangeAlias()
    {
        SetAlias(Alias);
        Save();
    }

    public void SetAlias(CommandAlias? newAlias)
    {
        _serviceProvider.GetService<AliasManager>()!.UpdateAlias(Id, newAlias);
        UpdateAlias();
        UpdateTags();
    }

    private void UpdateAlias()
    {
        // Add tags for the alias, if we have one.
        var aliases = _serviceProvider.GetService<AliasManager>();
        if (aliases != null)
        {
            Alias = aliases.AliasFromId(Id);
        }
    }

    private void UpdateHotkey()
    {
        var hotkey = _settings.CommandHotkeys.Where(hk => hk.CommandId == Id).FirstOrDefault();
        if (hotkey != null)
        {
            _hotkey = hotkey.Hotkey;
        }
    }

    private void UpdateTags()
    {
        List<Tag> tags = new();

        if (Hotkey != null)
        {
            tags.Add(new Tag() { Text = Hotkey.ToString() });
        }

        if (Alias != null)
        {
            tags.Add(new Tag() { Text = Alias.SearchPrefix });
        }

        PropChanged?.Invoke(this, new PropChangedEventArgs(nameof(Tags)));

        DoOnUiThread(
            () =>
            {
                ListHelpers.InPlaceUpdateList(Tags, tags);
            });
    }

    private void GenerateId()
    {
        // Use WyHash64 to generate stable ID hashes.
        // manually seeding with 0, so that the hash is stable across launches
        var result = WyHash64.ComputeHash64(_commandProviderId + Title + Subtitle, seed: 0);
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

    public void TryUpdateFallbackText(string newQuery)
    {
        if (!IsFallback)
        {
            return;
        }

        _ = Task.Run(() =>
        {
            try
            {
                var model = _commandItemViewModel.Model.Unsafe;
                if (model is IFallbackCommandItem fallback)
                {
                    var wasEmpty = string.IsNullOrEmpty(Title);
                    fallback.FallbackHandler.UpdateQuery(newQuery);
                    var isEmpty = string.IsNullOrEmpty(Title);
                    if (wasEmpty != isEmpty)
                    {
                        WeakReferenceMessenger.Default.Send<UpdateFallbackItemsMessage>();
                    }
                }
            }
            catch (Exception)
            {
            }
        });
    }
}
