// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Extensions.Helpers;

public partial class CommandItem : BaseObservable, ICommandItem
{
    private IconInfo? _icon;
    private string _title = string.Empty;
    private string _subtitle = string.Empty;
    private ICommand? _command;
    private IContextItem[] _moreCommands = [];

    public IconInfo? Icon
    {
        get => _icon ?? _command?.Icon;
        set
        {
            _icon = value;
            OnPropertyChanged(nameof(Icon));
        }
    }

    public string Title
    {
        get => !string.IsNullOrEmpty(this._title) ? _title : _command?.Name ?? string.Empty;

        set
        {
            _title = value;
            OnPropertyChanged(nameof(Title));
        }
    }

    public string Subtitle
    {
        get => _subtitle;
        set
        {
            _subtitle = value;
            OnPropertyChanged(nameof(Subtitle));
        }
    }

    public virtual ICommand? Command
    {
        get => _command;
        set
        {
            _command = value;
            OnPropertyChanged(nameof(Command));
        }
    }

    public IContextItem[] MoreCommands
    {
        get => _moreCommands;
        set
        {
            _moreCommands = value;
            OnPropertyChanged(nameof(MoreCommands));
        }
    }

    public CommandItem(ICommand command)
    {
        Command = command;
        Title = command.Name;
    }

    public CommandItem(ICommandItem other)
    {
        Command = other.Command;
        Title = other.Title;
        Subtitle = other.Subtitle;
        Icon = other.Icon;
        MoreCommands = other.MoreCommands;
    }
}
