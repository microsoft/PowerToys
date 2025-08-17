﻿// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class CommandItem : BaseObservable, ICommandItem
{
    private ICommand? _command;
    private WeakEventListener<CommandItem, object, IPropChangedEventArgs>? _commandListener;
    private string _title = string.Empty;

    public virtual IIconInfo? Icon
    {
        get => field;
        set
        {
            field = value;
            OnPropertyChanged(nameof(Icon));
        }
    }

    public virtual string Title
    {
        get => !string.IsNullOrEmpty(_title) ? _title : _command?.Name ?? string.Empty;

        set
        {
            _title = value;
            OnPropertyChanged(nameof(Title));
        }
    }

    public virtual string Subtitle
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged(nameof(Subtitle));
        }
    }

= string.Empty;

    public virtual ICommand? Command
    {
        get => _command;
        set
        {
            if (_commandListener != null)
            {
                _commandListener.Detach();
                _commandListener = null;
            }

            _command = value;

            if (value != null)
            {
                _commandListener = new(this, OnCommandPropertyChanged, listener => value.PropChanged -= listener.OnEvent);
                value.PropChanged += _commandListener.OnEvent;
            }

            OnPropertyChanged(nameof(Command));
            if (string.IsNullOrWhiteSpace(_title))
            {
                OnPropertyChanged(nameof(Title));
            }
        }
    }

    private static void OnCommandPropertyChanged(CommandItem instance, object source, IPropChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ICommand.Name))
        {
            instance.OnPropertyChanged(nameof(Title));
        }
    }

    public virtual IContextItem[] MoreCommands
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged(nameof(MoreCommands));
        }
    }

= [];

    public CommandItem()
        : this(new NoOpCommand())
    {
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
        Icon = (IconInfo?)other.Icon;
        MoreCommands = other.MoreCommands;
    }

    public CommandItem(
        string title,
        string subtitle = "",
        string name = "",
        Action? action = null,
        ICommandResult? result = null)
    {
        var c = new AnonymousCommand(action);
        if (!string.IsNullOrEmpty(name))
        {
            c.Name = name;
        }

        if (result != null)
        {
            c.Result = result;
        }

        Command = c;

        Title = title;
        Subtitle = subtitle;
    }
}
