// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class CommandItem : BaseObservable, ICommandItem
{
    private ICommand? _command;

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
        get => !string.IsNullOrEmpty(field) ? field : _command?.Name ?? string.Empty;

        set
        {
            field = value;
            OnPropertyChanged(nameof(Title));
        }
    }

= string.Empty;

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
            _command = value;
            OnPropertyChanged(nameof(Command));
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
