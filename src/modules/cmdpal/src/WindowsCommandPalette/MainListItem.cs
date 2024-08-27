// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Windows.CommandPalette.Extensions.Helpers;
using Microsoft.Windows.CommandPalette.Extensions;
using Windows.Foundation;
//using Quicklinks;
//using Scripts;

namespace DeveloperCommandPalette;

public sealed class MainListItem : ListItem
{
    private readonly IListItem _listItem;
    public IListItem Item => _listItem;
    internal MainListItem(IListItem listItem) : base(listItem.Command)
    {
        _listItem = listItem;

        _Title = _listItem.Title ?? _listItem.Command.Name;
        _Subtitle = _listItem.Subtitle;
        _MoreCommands = _listItem.MoreCommands;
        _FallbackHandler = _listItem.FallbackHandler;
        _Tags = _listItem.Tags;

        Command.PropChanged += Action_PropertyChanged;
        _listItem.PropChanged += Action_PropertyChanged;
    }

    private void Action_PropertyChanged(object sender, Microsoft.Windows.CommandPalette.Extensions.PropChangedEventArgs args)
    {
        if (args.PropertyName == "Name")
        {
            this.Title = !string.IsNullOrEmpty(this._listItem.Title) ? this._listItem.Title : Command.Name;
            OnPropertyChanged(nameof(Title));
        }
        else if (args.PropertyName == nameof(Title))
        {
            this.Title = this._listItem.Title;
        }
        else if (args.PropertyName == nameof(Subtitle))
        {
            this.Subtitle = this._listItem.Subtitle;
        }
        else if (args.PropertyName == nameof(MoreCommands))
        {
            this.MoreCommands = this._listItem.MoreCommands;
        }
        OnPropertyChanged(args.PropertyName);
    }
}
