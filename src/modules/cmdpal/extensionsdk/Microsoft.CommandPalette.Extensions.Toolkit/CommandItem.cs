// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation.Collections;
using WinRT;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class CommandItem : BaseObservable, ICommandItem
{
    // NOTE TO MAINTAINERS: Do NOT implement `IExtendedAttributesProvider` here
    // directly. Instead, implement it in derived classes like `ListItem` where
    // appropriate.
    //
    // Putting it directly here will cause out-of-proc extensions to fail to
    // load the context menu commands, for unknown CsWinRT reasons.
    private readonly PropertySet _extendedAttributes = new();

    private ICommand? _command;
    private WeakEventListener<CommandItem, object, IPropChangedEventArgs>? _commandListener;
    private string _title = string.Empty;

    private DataPackage? _dataPackage;
    private DataPackageView? _dataPackageView;

    public virtual IIconInfo? Icon { get; set => SetProperty(ref field, value); }

    public virtual string Title
    {
        get => !string.IsNullOrEmpty(_title) ? _title : _command?.Name ?? string.Empty;
        set
        {
            var oldTitle = Title;
            _title = value;
            if (Title != oldTitle)
            {
                OnPropertyChanged();
            }
        }
    }

    public virtual string Subtitle { get; set => SetProperty(ref field, value); } = string.Empty;

    public virtual ICommand? Command
    {
        get => _command;
        set
        {
            if (EqualityComparer<ICommand?>.Default.Equals(value, _command))
            {
                return;
            }

            var oldTitle = Title;

            // Unsubscribe the outgoing command explicitly. OnDetachAction only
            // runs once this CommandItem has been collected, so it can't cover
            // the case where the command is simply replaced.
            if (_commandListener is not null)
            {
                _command?.PropChanged -= _commandListener.OnEvent;
                _commandListener.Detach();
                _commandListener = null;
            }

            _command = value;

            if (value is not null)
            {
                // OnCommandPropertyChanged must be static so the delegate's Target is null.
                // An instance method group would bind `this` into OnEventAction,
                // giving the listener a strong ref back to this CommandItem and
                // defeating the weak reference. That was the actual leak.
                //
                // OnDetachAction does capture `value`, but that is not a leak: the
                // only thing keeping the listener alive is `value`'s own PropChanged
                // list, so the two form a cycle the GC reclaims together. Without it
                // the listener could never unsubscribe itself, and every collected
                // CommandItem would leave a dead handler on a long-lived command.
                _commandListener = new(this, OnCommandPropertyChanged, listener => value.PropChanged -= listener.OnEvent);
                value.PropChanged += _commandListener.OnEvent;
            }

            OnPropertyChanged();
            if (string.IsNullOrEmpty(_title) && oldTitle != Title)
            {
                OnPropertyChanged(nameof(Title));
            }
        }
    }

    private static void OnCommandPropertyChanged(CommandItem instance, object source, IPropChangedEventArgs args)
    {
        // command's name affects Title only if Title wasn't explicitly set
        if (args.PropertyName == nameof(ICommand.Name) && string.IsNullOrEmpty(instance._title))
        {
            instance.OnPropertyChanged(nameof(Title));
        }
    }

    public virtual IContextItem[] MoreCommands { get; set => SetProperty(ref field, value); } = [];

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
