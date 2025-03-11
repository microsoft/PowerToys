// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.DependencyInjection;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels;

public sealed partial class TopLevelViewModel : ObservableObject, IListItem
{
    private readonly SettingsModel _settings;
    private readonly IServiceProvider _serviceProvider;
    private readonly CommandItemViewModel _commandItemViewModel;
    private readonly string _idFromModel = string.Empty;

    private readonly string _generatedId = string.Empty;
    private HotkeySettings? _hotkey;

    private CommandAlias? Alias { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<Tag> Tags { get; set; } = [];

    public string Id => string.IsNullOrEmpty(_idFromModel) ? _generatedId : _idFromModel;

    string ICommandItem.Title => _commandItemViewModel.Title;

    string ICommandItem.Subtitle => _commandItemViewModel.Subtitle;

    IIconInfo ICommandItem.Icon => _commandItemViewModel.Icon;

    ITag[] IListItem.Tags => Tags.ToArray();

    IDetails? IListItem.Details => null;

    string IListItem.Section => string.Empty;

    string IListItem.TextToSuggest => string.Empty;

    ICommand? ICommandItem.Command => _commandItemViewModel.Command.Model.Unsafe;

    IContextItem?[] ICommandItem.MoreCommands => _commandItemViewModel.MoreCommands.Select(i => i.Model.Unsafe).ToArray()

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
        SettingsModel settings,
        IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _settings = settings;

        _commandItemViewModel = item;

        item.PropertyChanged += Item_PropertyChanged;

        UpdateAlias();
    }

    private void Item_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.PropertyName))
        {
            PropChanged?.Invoke(this, new PropChangedEventArgs(e.PropertyName));
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
        var settings = _serviceProvider.GetService<SettingsModel>()!;
        var hotkey = settings.CommandHotkeys.Where(hk => hk.CommandId == Id).FirstOrDefault();
        if (hotkey != null)
        {
            _hotkey = hotkey.Hotkey;
        }
    }

    private void UpdateTags()
    {
        var tags = new List<Tag>();

        if (Hotkey != null)
        {
            tags.Add(new Tag() { Text = Hotkey.ToString() });
        }

        if (Alias != null)
        {
            tags.Add(new Tag() { Text = Alias.SearchPrefix });
        }

        Task.Factory.StartNew(
            () =>
            {
                ListHelpers.InPlaceUpdateList(Tags, tags);
            },
            CancellationToken.None,
            TaskCreationOptions.None,
            _commandItemViewModel.PageContext.Scheduler);
    }
}
