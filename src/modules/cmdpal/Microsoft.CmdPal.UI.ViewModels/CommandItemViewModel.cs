// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class CommandItemViewModel : ExtensionObjectViewModel, ICommandBarContext
{
    public ExtensionObject<ICommandItem> Model => _commandItemModel;

    private readonly ExtensionObject<ICommandItem> _commandItemModel = new(null);
    private CommandContextItemViewModel? _defaultCommandContextItem;

    internal InitializedState Initialized { get; private set; } = InitializedState.Uninitialized;

    protected bool IsFastInitialized => IsInErrorState || Initialized.HasFlag(InitializedState.FastInitialized);

    protected bool IsInitialized => IsInErrorState || Initialized.HasFlag(InitializedState.Initialized);

    protected bool IsSelectedInitialized => IsInErrorState || Initialized.HasFlag(InitializedState.SelectionInitialized);

    public bool IsInErrorState => Initialized.HasFlag(InitializedState.Error);

    // These are properties that are "observable" from the extension object
    // itself, in the sense that they get raised by PropChanged events from the
    // extension. However, we don't want to actually make them
    // [ObservableProperty]s, because PropChanged comes in off the UI thread,
    // and ObservableProperty is not smart enough to raise the PropertyChanged
    // on the UI thread.
    public string Name => Command.Name;

    private string _itemTitle = string.Empty;

    public string Title => string.IsNullOrEmpty(_itemTitle) ? Name : _itemTitle;

    public string Subtitle { get; private set; } = string.Empty;

    private IconInfoViewModel _listItemIcon = new(null);

    public IconInfoViewModel Icon => _listItemIcon.IsSet ? _listItemIcon : Command.Icon;

    public CommandViewModel Command { get; private set; }

    public List<CommandContextItemViewModel> MoreCommands { get; private set; } = [];

    IEnumerable<CommandContextItemViewModel> ICommandBarContext.MoreCommands => MoreCommands;

    public bool HasMoreCommands => MoreCommands.Count > 0;

    public string SecondaryCommandName => SecondaryCommand?.Name ?? string.Empty;

    public CommandItemViewModel? PrimaryCommand => this;

    public CommandItemViewModel? SecondaryCommand => HasMoreCommands ? MoreCommands[0] : null;

    public bool ShouldBeVisible => !string.IsNullOrEmpty(Name);

    public List<CommandContextItemViewModel> AllCommands
    {
        get
        {
            List<CommandContextItemViewModel> l = _defaultCommandContextItem == null ?
                new() :
                [_defaultCommandContextItem];

            l.AddRange(MoreCommands);
            return l;
        }
    }

    private static readonly IconInfoViewModel _errorIcon;

    static CommandItemViewModel()
    {
        _errorIcon = new(new IconInfo("\uEA39")); // ErrorBadge
        _errorIcon.InitializeProperties();
    }

    public CommandItemViewModel(ExtensionObject<ICommandItem> item, WeakReference<IPageContext> errorContext)
        : base(errorContext)
    {
        _commandItemModel = item;
        Command = new(null, errorContext);
    }

    public void FastInitializeProperties()
    {
        if (IsFastInitialized)
        {
            return;
        }

        var model = _commandItemModel.Unsafe;
        if (model == null)
        {
            return;
        }

        Command = new(model.Command, PageContext);
        Command.FastInitializeProperties();

        _itemTitle = model.Title;
        Subtitle = model.Subtitle;

        Initialized |= InitializedState.FastInitialized;
    }

    //// Called from ListViewModel on background thread started in ListPage.xaml.cs
    public override void InitializeProperties()
    {
        if (IsInitialized)
        {
            return;
        }

        if (!IsFastInitialized)
        {
            FastInitializeProperties();
        }

        var model = _commandItemModel.Unsafe;
        if (model == null)
        {
            return;
        }

        Command.InitializeProperties();

        var listIcon = model.Icon;
        if (listIcon != null)
        {
            _listItemIcon = new(listIcon);
            _listItemIcon.InitializeProperties();
        }

        // TODO: Do these need to go into FastInit?
        model.PropChanged += Model_PropChanged;
        Command.PropertyChanged += Command_PropertyChanged;

        UpdateProperty(nameof(Name));
        UpdateProperty(nameof(Title));
        UpdateProperty(nameof(Subtitle));
        UpdateProperty(nameof(Icon));

        // Load-bearing: if you don't raise a IsInitialized here, then
        // TopLevelViewModel will never know what the command's ID is, so it
        // will never be able to load Hotkeys & aliases
        UpdateProperty(nameof(IsInitialized));

        Initialized |= InitializedState.Initialized;
    }

    public void SlowInitializeProperties()
    {
        if (IsSelectedInitialized)
        {
            return;
        }

        if (!IsInitialized)
        {
            InitializeProperties();
        }

        var model = _commandItemModel.Unsafe;
        if (model == null)
        {
            return;
        }

        var more = model.MoreCommands;
        if (more != null)
        {
            MoreCommands = more
                .Where(contextItem => contextItem is ICommandContextItem)
                .Select(contextItem => (contextItem as ICommandContextItem)!)
                .Select(contextItem => new CommandContextItemViewModel(contextItem, PageContext))
                .ToList();
        }

        // Here, we're already theoretically in the async context, so we can
        // use Initialize straight up
        MoreCommands.ForEach(contextItem =>
        {
            contextItem.InitializeProperties();
        });

        _defaultCommandContextItem = new(new CommandContextItem(model.Command!), PageContext)
        {
            _itemTitle = Name,
            Subtitle = Subtitle,
            Command = Command,

            // TODO this probably should just be a CommandContextItemViewModel(CommandItemViewModel) ctor, or a copy ctor or whatever
        };

        // Only set the icon on the context item for us if our command didn't
        // have its own icon
        if (!Command.HasIcon)
        {
            _defaultCommandContextItem._listItemIcon = _listItemIcon;
        }

        Initialized |= InitializedState.SelectionInitialized;
        UpdateProperty(nameof(MoreCommands));
        UpdateProperty(nameof(AllCommands));
        UpdateProperty(nameof(IsSelectedInitialized));
    }

    public bool SafeFastInit()
    {
        try
        {
            FastInitializeProperties();
            return true;
        }
        catch (Exception)
        {
            Command = new(null, PageContext);
            _itemTitle = "Error";
            Subtitle = "Item failed to load";
            MoreCommands = [];
            _listItemIcon = _errorIcon;
            Initialized |= InitializedState.Error;
        }

        return false;
    }

    public bool SafeSlowInit()
    {
        try
        {
            SlowInitializeProperties();
            return true;
        }
        catch (Exception)
        {
            Initialized |= InitializedState.Error;
        }

        return false;
    }

    public bool SafeInitializeProperties()
    {
        try
        {
            InitializeProperties();
            return true;
        }
        catch (Exception)
        {
            Command = new(null, PageContext);
            _itemTitle = "Error";
            Subtitle = "Item failed to load";
            MoreCommands = [];
            _listItemIcon = _errorIcon;
            Initialized |= InitializedState.Error;
        }

        return false;
    }

    private void Model_PropChanged(object sender, IPropChangedEventArgs args)
    {
        try
        {
            FetchProperty(args.PropertyName);
        }
        catch (Exception ex)
        {
            ShowException(ex, _commandItemModel?.Unsafe?.Title);
        }
    }

    protected virtual void FetchProperty(string propertyName)
    {
        var model = this._commandItemModel.Unsafe;
        if (model == null)
        {
            return; // throw?
        }

        switch (propertyName)
        {
            case nameof(Command):
                if (Command != null)
                {
                    Command.PropertyChanged -= Command_PropertyChanged;
                }

                Command = new(model.Command, PageContext);
                Command.InitializeProperties();
                UpdateProperty(nameof(Name));
                UpdateProperty(nameof(Title));
                UpdateProperty(nameof(Icon));
                break;

            case nameof(Title):
                _itemTitle = model.Title;
                break;

            case nameof(Subtitle):
                this.Subtitle = model.Subtitle;
                break;

            case nameof(Icon):
                _listItemIcon = new(model.Icon);
                _listItemIcon.InitializeProperties();
                break;

            case nameof(model.MoreCommands):
                var more = model.MoreCommands;
                if (more != null)
                {
                    var newContextMenu = more
                        .Where(contextItem => contextItem is ICommandContextItem)
                        .Select(contextItem => (contextItem as ICommandContextItem)!)
                        .Select(contextItem => new CommandContextItemViewModel(contextItem, PageContext))
                        .ToList();
                    lock (MoreCommands)
                    {
                        ListHelpers.InPlaceUpdateList(MoreCommands, newContextMenu);
                    }

                    newContextMenu.ForEach(contextItem =>
                    {
                        contextItem.InitializeProperties();
                    });
                }
                else
                {
                    lock (MoreCommands)
                    {
                        MoreCommands.Clear();
                    }
                }

                UpdateProperty(nameof(SecondaryCommand));
                UpdateProperty(nameof(SecondaryCommandName));
                UpdateProperty(nameof(HasMoreCommands));

                break;
        }

        UpdateProperty(propertyName);
    }

    private void Command_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        var propertyName = e.PropertyName;
        switch (propertyName)
        {
            case nameof(Command.Name):
                UpdateProperty(nameof(Title));
                UpdateProperty(nameof(Name));
                break;
            case nameof(Command.Icon):
                UpdateProperty(nameof(Icon));
                break;
        }
    }

    protected override void UnsafeCleanup()
    {
        base.UnsafeCleanup();

        lock (MoreCommands)
        {
            MoreCommands.ForEach(c => c.SafeCleanup());
            MoreCommands.Clear();
        }

        // _listItemIcon.SafeCleanup();
        _listItemIcon = new(null); // necessary?

        _defaultCommandContextItem?.SafeCleanup();
        _defaultCommandContextItem = null;

        Command.PropertyChanged -= Command_PropertyChanged;
        Command.SafeCleanup();

        var model = _commandItemModel.Unsafe;
        if (model != null)
        {
            model.PropChanged -= Model_PropChanged;
        }
    }

    public override void SafeCleanup()
    {
        base.SafeCleanup();
        Initialized |= InitializedState.CleanedUp;
    }
}

[Flags]
internal enum InitializedState
{
    Uninitialized = 0,
    FastInitialized = 1,
    Initialized = 2,
    SelectionInitialized = 4,
    Error = 8,
    CleanedUp = 16,
}
