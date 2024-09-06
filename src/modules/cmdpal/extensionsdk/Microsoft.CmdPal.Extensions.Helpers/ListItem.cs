// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel.DataAnnotations;

namespace Microsoft.CmdPal.Extensions.Helpers;

public class ListItem : BaseObservable, IListItem
{
    private IconDataType? _icon;
    private string _title = string.Empty;
    private string _subtitle = string.Empty;
    private ITag[] _tags = [];
    private IDetails? _details;
    private ICommand? _command;
    private IContextItem[] _moreCommands = [];
    private IFallbackHandler? _fallbackHandler;

    public IconDataType? Icon
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
        get
        {
            if (!string.IsNullOrEmpty(this._title))
            {
                return _title;
            }
            else
            {
                return _command?.Name ?? string.Empty;
            }
        }
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

    public ITag[] Tags
    {
        get => _tags;
        set
        {
            _tags = value;
            OnPropertyChanged(nameof(Tags));
        }
    }

    public IDetails? Details
    {
        get => _details;
        set
        {
            _details = value;
            OnPropertyChanged(nameof(Details));
        }
    }

    public ICommand? Command
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

    public IFallbackHandler? FallbackHandler
    {
        get => _fallbackHandler ?? _command as IFallbackHandler;
        init => _fallbackHandler = value;
    }

    public ListItem(ICommand command)
    {
        Command = command;
        Title = command.Name;
    }
}
