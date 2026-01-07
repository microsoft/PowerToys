// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation.Collections;
using WinRT;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class CommandItem : BaseObservable, ICommandItem, IExtendedAttributesProvider
{
    private readonly PropertySet _extendedAttributes = new();

    private ICommand? _command;
    private WeakEventListener<CommandItem, object, IPropChangedEventArgs>? _commandListener;
    private string _title = string.Empty;

    private DataPackage? _dataPackage;
    private DataPackageView? _dataPackageView;

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
            if (_commandListener is not null)
            {
                _commandListener.Detach();
                _commandListener = null;
            }

            _command = value;

            if (value is not null)
            {
                _commandListener = new(this, OnCommandPropertyChanged, listener => value.PropChanged -= listener.OnEvent);
                value.PropChanged += _commandListener.OnEvent;
            }

            OnPropertyChanged(nameof(Command));
            if (string.IsNullOrEmpty(_title))
            {
                OnPropertyChanged(nameof(Title));
            }
        }
    }

    private void OnCommandPropertyChanged(CommandItem instance, object source, IPropChangedEventArgs args)
    {
        // command's name affects Title only if Title wasn't explicitly set
        if (args.PropertyName == nameof(ICommand.Name) && string.IsNullOrEmpty(_title))
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

    public DataPackage? DataPackage
    {
        get => _dataPackage;
        set
        {
            _dataPackage = value;
            _dataPackageView = null;
            _extendedAttributes[WellKnownExtensionAttributes.DataPackage] = value?.AsAgile().Get()?.GetView()!;
            OnPropertyChanged(nameof(DataPackage));
            OnPropertyChanged(nameof(DataPackageView));
        }
    }

    public DataPackageView? DataPackageView
    {
        get => _dataPackageView;
        set
        {
            _dataPackage = null;
            _dataPackageView = value;
            _extendedAttributes[WellKnownExtensionAttributes.DataPackage] = value?.AsAgile().Get()!;
            OnPropertyChanged(nameof(DataPackage));
            OnPropertyChanged(nameof(DataPackageView));
        }
    }

    public CommandItem()
        : this(new NoOpCommand())
    {
    }

    public CommandItem(ICommand command)
    {
        Command = command;
    }

    public CommandItem(ICommandItem other)
    {
        Command = other.Command;
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

        if (result is not null)
        {
            c.Result = result;
        }

        Command = c;

        Title = title;
        Subtitle = subtitle;
    }

    public IDictionary<string, object> GetProperties()
    {
        return _extendedAttributes;
    }
}
