// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.CmdPal.Common;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.ApplicationModel.DataTransfer;

namespace Microsoft.CmdPal.UI.ViewModels;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
public partial class CommandItemViewModel : ExtensionObjectViewModel, ICommandBarContext, IPrecomputedListItem
{
    public ExtensionObject<ICommandItem> Model => _commandItemModel;

    private readonly IContextMenuFactory? _contextMenuFactory;

    private readonly Lock _moreCommandsLock = new();
    private readonly List<IContextItemViewModel> _moreCommands = [];
    private volatile CommandContextItemViewModel? _secondaryMoreCommand;
    private volatile IContextItemViewModel[] _moreCommandsSnapshot = [];
    private volatile IContextItemViewModel[] _allCommandsSnapshot = [];

    private ExtensionObject<IExtendedAttributesProvider>? ExtendedAttributesProvider { get; set; }

    private readonly ExtensionObject<ICommandItem> _commandItemModel = new(null);
    private CommandContextItemViewModel? _defaultCommandContextItemViewModel;

    private FuzzyTargetCache _titleCache;
    private FuzzyTargetCache _subtitleCache;

    internal InitializedState Initialized { get; private set; } = InitializedState.Uninitialized;

    protected bool IsFastInitialized => IsInErrorState || Initialized.HasFlag(InitializedState.FastInitialized);

    protected bool IsInitialized => IsInErrorState || Initialized.HasFlag(InitializedState.Initialized);

    protected bool IsSelectedInitialized => IsInErrorState || Initialized.HasFlag(InitializedState.SelectionInitialized);

    public bool IsContextMenuItem { get; protected init; }

    public bool IsInErrorState => Initialized.HasFlag(InitializedState.Error);

    // These are properties that are "observable" from the extension object
    // itself, in the sense that they get raised by PropChanged events from the
    // extension. However, we don't want to actually make them
    // [ObservableProperty]s, because PropChanged comes in off the UI thread,
    // and ObservableProperty is not smart enough to raise the PropertyChanged
    // on the UI thread.
    public string Name => Command.Name;

    private string _itemTitle = string.Empty;

    protected string ItemTitle => _itemTitle;

    public virtual string Title => string.IsNullOrEmpty(_itemTitle) ? Name : _itemTitle;

    public virtual string Subtitle { get; private set; } = string.Empty;

    private IconInfoViewModel _icon = new(null);

    public IconInfoViewModel Icon => _icon.IsSet ? _icon : Command.Icon;

    public CommandViewModel Command { get; private set; }

    // Reuse a cached read-only snapshot so repeated reads don't allocate.
    public IReadOnlyList<IContextItemViewModel> MoreCommands => _moreCommandsSnapshot;

    IReadOnlyList<IContextItemViewModel> IContextMenuContext.MoreCommands => _moreCommandsSnapshot;

    protected Lock MoreCommandsLock => _moreCommandsLock;

    protected List<IContextItemViewModel> UnsafeMoreCommands => _moreCommands;

    public bool HasMoreCommands => _secondaryMoreCommand is not null;

    public string SecondaryCommandName => _secondaryMoreCommand?.Name ?? string.Empty;

    public CommandItemViewModel? PrimaryCommand => this;

    public CommandItemViewModel? SecondaryCommand => _secondaryMoreCommand;

    public bool ShouldBeVisible => !string.IsNullOrEmpty(Name);

    public bool HasTitle => !string.IsNullOrEmpty(Title);

    public bool HasSubtitle => !string.IsNullOrEmpty(Subtitle);

    public virtual bool HasText => HasTitle || HasSubtitle;

    public DataPackageView? DataPackage { get; private set; }

    public IReadOnlyList<IContextItemViewModel> AllCommands => _allCommandsSnapshot;

    private static readonly IconInfoViewModel _errorIcon;

    static CommandItemViewModel()
    {
        _errorIcon = new(new IconInfo("\uEA39")); // ErrorBadge
        _errorIcon.InitializeProperties();
    }

    public CommandItemViewModel(
        ExtensionObject<ICommandItem> item,
        WeakReference<IPageContext> errorContext,
        IContextMenuFactory? contextMenuFactory)
        : base(errorContext)
    {
        _commandItemModel = item;
        _contextMenuFactory = contextMenuFactory;
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
        _titleCache.Invalidate();
        _subtitleCache.Invalidate();

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

        if (model is IExtendedAttributesProvider extendedAttributesProvider)
        {
            ExtendedAttributesProvider = new ExtensionObject<IExtendedAttributesProvider>(extendedAttributesProvider);
            var properties = extendedAttributesProvider.GetProperties();
            UpdateDataPackage(properties);
        }

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

        BuildAndInitMoreCommands();

        TryCreateDefaultCommandContextItem(model);

        lock (_moreCommandsLock)
        {
            RefreshMoreCommandStateUnsafe();
        }

        Initialized |= InitializedState.SelectionInitialized;
        UpdateProperty(nameof(MoreCommands));
        UpdateProperty(nameof(AllCommands));
        UpdateProperty(nameof(SecondaryCommand), nameof(SecondaryCommandName), nameof(HasMoreCommands));
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
            ClearMoreCommands();
            _icon = _errorIcon;
            _titleCache.Invalidate();
            _subtitleCache.Invalidate();
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
            ClearMoreCommands();
            _icon = _errorIcon;
            _titleCache.Invalidate();
            _subtitleCache.Invalidate();
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
                Command.PropertyChanged += Command_PropertyChanged;

                // Extensions based on Command Palette SDK < 0.3 CommandItem class won't notify when Title changes because Command
                // or Command.Name change. This is a workaround to ensure that the Title is always up-to-date for extensions with old SDK.
                _itemTitle = model.Title;

                if (_defaultCommandContextItemViewModel is not null)
                {
                    _defaultCommandContextItemViewModel.Command = Command;
                    _defaultCommandContextItemViewModel.UpdateTitle(_itemTitle);
                    UpdateDefaultContextItemIcon();
                }
                else
                {
                    TryCreateDefaultCommandContextItem(model);
                }

                UpdateProperty(nameof(Name));
                UpdateProperty(nameof(Title));
                UpdateProperty(nameof(Icon));
                UpdateProperty(nameof(HasText));
                break;

            case nameof(Title):
                _itemTitle = model.Title;
                _titleCache.Invalidate();
                UpdateProperty(nameof(HasText));
                break;

            case nameof(Subtitle):
                var modelSubtitle = model.Subtitle;
                this.Subtitle = modelSubtitle;
                _defaultCommandContextItemViewModel?.Subtitle = modelSubtitle;
                _subtitleCache.Invalidate();
                UpdateProperty(nameof(HasText));
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
                BuildAndInitMoreCommands();
                UpdateProperty(nameof(SecondaryCommand), nameof(SecondaryCommandName), nameof(HasMoreCommands), nameof(AllCommands));

                break;
            case nameof(DataPackage):
                UpdateDataPackage(ExtendedAttributesProvider?.Unsafe?.GetProperties());
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
                _titleCache.Invalidate();
                UpdateProperty(nameof(Title), nameof(Name));

                if (_defaultCommandContextItemViewModel is not null)
                {
                    _defaultCommandContextItemViewModel.UpdateTitle(model.Command.Name);
                }
                else
                {
                    TryCreateDefaultCommandContextItem(model);
                }

                break;

            case nameof(Command.Icon):
                UpdateDefaultContextItemIcon();
                UpdateProperty(nameof(Icon));
                break;
        }
    }

    /// <summary>
    /// Creates <see cref="_defaultCommandContextItemViewModel"/> when it does not exist
    /// yet and the current command has a non-empty name. This covers the case
    /// where an extension initially exposes a <c>NoOpCommand</c> (empty name)
    /// and later switches to a concrete command after <see cref="SlowInitializeProperties"/> has already run.
    /// When a new instance is created, the snapshot is refreshed and
    /// <see cref="AllCommands"/> is notified.
    /// </summary>
    private void TryCreateDefaultCommandContextItem(ICommandItem model)
    {
        if (_defaultCommandContextItemViewModel is not null)
        {
            return;
        }

        if (string.IsNullOrEmpty(model.Command?.Name))
        {
            return;
        }

        _defaultCommandContextItemViewModel = new CommandContextItemViewModel(new CommandContextItem(model.Command!), PageContext)
        {
            _itemTitle = Name,
            Subtitle = Subtitle,
            Command = Command,

            // TODO this probably should just be a CommandContextItemViewModel(CommandItemViewModel) ctor, or a copy ctor or whatever
            // Anything we set manually here must stay in sync with the corresponding properties on CommandItemViewModel.
        };

        UpdateDefaultContextItemIcon();

        lock (_moreCommandsLock)
        {
            RefreshMoreCommandStateUnsafe();
        }

        UpdateProperty(nameof(AllCommands));
    }

    private void UpdateDefaultContextItemIcon() =>

        // Command icon takes precedence over our icon on the primary command
        _defaultCommandContextItemViewModel?.UpdateIcon(Command.Icon.IsSet ? Command.Icon : _icon);

    private void UpdateTitle(string? title)
    {
        _itemTitle = title ?? string.Empty;
        _titleCache.Invalidate();
        UpdateProperty(nameof(Title));
    }

    private void UpdateIcon(IIconInfo? iconInfo)
    {
        _icon = new(iconInfo);
        _icon.InitializeProperties();
        UpdateProperty(nameof(Icon));
    }

    private void UpdateDataPackage(IDictionary<string, object?>? properties)
    {
        DataPackage =
            properties?.TryGetValue(WellKnownExtensionAttributes.DataPackage, out var dataPackageView) == true &&
            dataPackageView is DataPackageView view
                ? view
                : null;
        UpdateProperty(nameof(DataPackage));
    }

    public FuzzyTarget GetTitleTarget(IPrecomputedFuzzyMatcher matcher)
        => _titleCache.GetOrUpdate(matcher, Title);

    public FuzzyTarget GetSubtitleTarget(IPrecomputedFuzzyMatcher matcher)
        => _subtitleCache.GetOrUpdate(matcher, Subtitle);

    /// <remarks>
    /// * Does call SlowInitializeProperties on the created items.
    /// * does NOT call UpdateProperty ; caller must do that.
    /// </remarks>
    private void BuildAndInitMoreCommands()
    {
        var model = _commandItemModel.Unsafe;
        if (model is null)
        {
            return;
        }

        var more = model.MoreCommands;
        var factory = _contextMenuFactory ?? DefaultContextMenuFactory.Instance;
        var results = factory.UnsafeBuildAndInitMoreCommands(more, this);

        List<IContextItemViewModel>? freedItems;
        lock (_moreCommandsLock)
        {
            ListHelpers.InPlaceUpdateList(_moreCommands, results, out freedItems);
            RefreshMoreCommandStateUnsafe();
        }

        freedItems.OfType<CommandContextItemViewModel>()
                  .ToList()
                  .ForEach(c => c.SafeCleanup());
    }

    public void RefreshMoreCommands()
    {
        Task.Run(RefreshMoreCommandsSynchronous);
    }

    private void RefreshMoreCommandsSynchronous()
    {
        try
        {
            BuildAndInitMoreCommands();
            UpdateProperty(nameof(MoreCommands));
            UpdateProperty(nameof(AllCommands));
            UpdateProperty(nameof(SecondaryCommand));
            UpdateProperty(nameof(SecondaryCommandName));
            UpdateProperty(nameof(HasMoreCommands));
        }
        catch (Exception ex)
        {
            // Handle any exceptions that might occur during the refresh process
            CoreLogger.LogError("Error refreshing MoreCommands in CommandItemViewModel", ex);
            ShowException(ex, _commandItemModel?.Unsafe?.Title);
        }
    }

    protected override void UnsafeCleanup()
    {
        base.UnsafeCleanup();

        List<IContextItemViewModel> freedItems;
        CommandContextItemViewModel? freedDefault;
        lock (_moreCommandsLock)
        {
            freedItems = [.. _moreCommands];
            _moreCommands.Clear();

            // Null out here so the single RefreshMoreCommandStateUnsafe call
            // produces an _allCommandsSnapshot that excludes the default command.
            freedDefault = _defaultCommandContextItemViewModel;
            _defaultCommandContextItemViewModel = null;

            RefreshMoreCommandStateUnsafe();
        }

        // Cleanup outside lock to avoid holding it during RPC calls
        freedItems.OfType<CommandContextItemViewModel>()
                  .ToList()
                  .ForEach(c => c.SafeCleanup());
        freedDefault?.SafeCleanup();

        // _listItemIcon.SafeCleanup();
        _icon = new(null); // necessary?

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

    protected void RefreshMoreCommandStateUnsafe()
    {
        _moreCommandsSnapshot = [.. _moreCommands];

        _secondaryMoreCommand = null;
        foreach (var item in _moreCommands)
        {
            if (item is CommandContextItemViewModel command)
            {
                _secondaryMoreCommand = command;
                break;
            }
        }

        _allCommandsSnapshot = _defaultCommandContextItemViewModel is null ?
            _moreCommandsSnapshot :
            [_defaultCommandContextItemViewModel, .. _moreCommandsSnapshot];
    }

    private void ClearMoreCommands()
    {
        List<IContextItemViewModel> freedItems;
        lock (_moreCommandsLock)
        {
            freedItems = [.. _moreCommands];
            _moreCommands.Clear();
            RefreshMoreCommandStateUnsafe();
        }

        freedItems.OfType<CommandContextItemViewModel>()
                  .ToList()
                  .ForEach(c => c.SafeCleanup());
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
