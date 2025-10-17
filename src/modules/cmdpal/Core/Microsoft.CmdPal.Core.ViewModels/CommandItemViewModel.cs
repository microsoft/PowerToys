// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.CmdPal.Core.Common;
using Microsoft.CmdPal.Core.ViewModels.Messages;
using Microsoft.CmdPal.Core.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Core.ViewModels;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
public partial class CommandItemViewModel : ExtensionObjectViewModel, ICommandBarContext
{
    public ExtensionObject<ICommandItem> Model => _commandItemModel;

    private readonly ExtensionObject<ICommandItem> _commandItemModel = new(null);
    private CommandContextItemViewModel? _defaultCommandContextItemViewModel;

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

    private IconInfoViewModel _icon = new(null);

    public IconInfoViewModel Icon => _icon.IsSet ? _icon : Command.Icon;

    public CommandViewModel Command { get; private set; }

    public List<IContextItemViewModel> MoreCommands { get; private set; } = [];

    IEnumerable<IContextItemViewModel> IContextMenuContext.MoreCommands => MoreCommands;

    private List<CommandContextItemViewModel> ActualCommands => MoreCommands.OfType<CommandContextItemViewModel>().ToList();

    public bool HasMoreCommands => ActualCommands.Count > 0;

    public string SecondaryCommandName => SecondaryCommand?.Name ?? string.Empty;

    public CommandItemViewModel? PrimaryCommand => this;

    public CommandItemViewModel? SecondaryCommand => HasMoreCommands ? ActualCommands[0] : null;

    public bool ShouldBeVisible => !string.IsNullOrEmpty(Name);

    public List<IContextItemViewModel> AllCommands
    {
        get
        {
            List<IContextItemViewModel> l = _defaultCommandContextItemViewModel is null ?
                new() :
                [_defaultCommandContextItemViewModel];

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
        if (model is null)
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
        if (model is null)
        {
            return;
        }

        Command.InitializeProperties();

        var icon = model.Icon;
        if (icon is not null)
        {
            _icon = new(icon);
            _icon.InitializeProperties();
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

    public virtual void SlowInitializeProperties()
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
        if (model is null)
        {
            return;
        }

        var more = model.MoreCommands;
        if (more is not null)
        {
            MoreCommands = more
                .Select<IContextItem, IContextItemViewModel>(item =>
                {
                    return item is ICommandContextItem contextItem ? new CommandContextItemViewModel(contextItem, PageContext) : new SeparatorViewModel();
                })
                .ToList();
        }

        // Here, we're already theoretically in the async context, so we can
        // use Initialize straight up
        MoreCommands
            .OfType<CommandContextItemViewModel>()
            .ToList()
            .ForEach(contextItem =>
            {
                contextItem.SlowInitializeProperties();
            });

        if (!string.IsNullOrEmpty(model.Command?.Name))
        {
            _defaultCommandContextItemViewModel = new CommandContextItemViewModel(new CommandContextItem(model.Command!), PageContext)
            {
                _itemTitle = Name,
                Subtitle = Subtitle,
                Command = Command,

                // TODO this probably should just be a CommandContextItemViewModel(CommandItemViewModel) ctor, or a copy ctor or whatever
                // Anything we set manually here must stay in sync with the corresponding properties on CommandItemViewModel.
            };

            // Only set the icon on the context item for us if our command didn't
            // have its own icon
            UpdateDefaultContextItemIcon();
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
        catch (Exception ex)
        {
            CoreLogger.LogError("error fast initializing CommandItemViewModel", ex);
            Command = new(null, PageContext);
            _itemTitle = "Error";
            Subtitle = "Item failed to load";
            MoreCommands = [];
            _icon = _errorIcon;
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
        catch (Exception ex)
        {
            Initialized |= InitializedState.Error;
            CoreLogger.LogError("error slow initializing CommandItemViewModel", ex);
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
        catch (Exception ex)
        {
            CoreLogger.LogError("error initializing CommandItemViewModel", ex);
            Command = new(null, PageContext);
            _itemTitle = "Error";
            Subtitle = "Item failed to load";
            MoreCommands = [];
            _icon = _errorIcon;
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
        if (model is null)
        {
            return; // throw?
        }

        switch (propertyName)
        {
            case nameof(Command):
                Command.PropertyChanged -= Command_PropertyChanged;
                Command = new(model.Command, PageContext);
                Command.InitializeProperties();

                // Extensions based on Command Palette SDK < 0.3 CommandItem class won't notify when Title changes because Command
                // or Command.Name change. This is a workaround to ensure that the Title is always up-to-date for extensions with old SDK.
                _itemTitle = model.Title;

                _defaultCommandContextItemViewModel?.Command = Command;
                _defaultCommandContextItemViewModel?.UpdateTitle(_itemTitle);
                UpdateDefaultContextItemIcon();

                UpdateProperty(nameof(Name));
                UpdateProperty(nameof(Title));
                UpdateProperty(nameof(Icon));
                break;

            case nameof(Title):
                _itemTitle = model.Title;
                break;

            case nameof(Subtitle):
                var modelSubtitle = model.Subtitle;
                this.Subtitle = modelSubtitle;
                _defaultCommandContextItemViewModel?.Subtitle = modelSubtitle;
                break;

            case nameof(Icon):
                var oldIcon = _icon;
                _icon = new(model.Icon);
                _icon.InitializeProperties();
                if (oldIcon.IsSet || _icon.IsSet)
                {
                    UpdateProperty(nameof(Icon));
                }

                UpdateDefaultContextItemIcon();

                break;

            case nameof(model.MoreCommands):
                var more = model.MoreCommands;
                if (more is not null)
                {
                    var newContextMenu = more
                        .Select<IContextItem, IContextItemViewModel>(item =>
                        {
                            return item is ICommandContextItem contextItem ? new CommandContextItemViewModel(contextItem, PageContext) : new SeparatorViewModel();
                        })
                        .ToList();
                    lock (MoreCommands)
                    {
                        ListHelpers.InPlaceUpdateList(MoreCommands, newContextMenu);
                    }

                    newContextMenu
                        .OfType<CommandContextItemViewModel>()
                        .ToList()
                        .ForEach(contextItem =>
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
        var model = _commandItemModel.Unsafe;
        if (model is null)
        {
            return;
        }

        switch (propertyName)
        {
            case nameof(Command.Name):
                // Extensions based on Command Palette SDK < 0.3 CommandItem class won't notify when Title changes because Command
                // or Command.Name change. This is a workaround to ensure that the Title is always up-to-date for extensions with old SDK.
                _itemTitle = model.Title;
                UpdateProperty(nameof(Title), nameof(Name));

                _defaultCommandContextItemViewModel?.UpdateTitle(model.Command.Name);
                break;

            case nameof(Command.Icon):
                UpdateDefaultContextItemIcon();
                UpdateProperty(nameof(Icon));
                break;
        }
    }

    private void UpdateDefaultContextItemIcon()
    {
        // Command icon takes precedence over our icon on the primary command
        _defaultCommandContextItemViewModel?.UpdateIcon(Command.Icon.IsSet ? Command.Icon : _icon);
    }

    private void UpdateTitle(string? title)
    {
        _itemTitle = title ?? string.Empty;
        UpdateProperty(nameof(Title));
    }

    private void UpdateIcon(IIconInfo? iconInfo)
    {
        _icon = new(iconInfo);
        _icon.InitializeProperties();
        UpdateProperty(nameof(Icon));
    }

    protected override void UnsafeCleanup()
    {
        base.UnsafeCleanup();

        lock (MoreCommands)
        {
            MoreCommands.OfType<CommandContextItemViewModel>()
                        .ToList()
                        .ForEach(c => c.SafeCleanup());
            MoreCommands.Clear();
        }

        // _listItemIcon.SafeCleanup();
        _icon = new(null); // necessary?

        _defaultCommandContextItemViewModel?.SafeCleanup();
        _defaultCommandContextItemViewModel = null;

        Command.PropertyChanged -= Command_PropertyChanged;
        Command.SafeCleanup();

        var model = _commandItemModel.Unsafe;
        if (model is not null)
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
