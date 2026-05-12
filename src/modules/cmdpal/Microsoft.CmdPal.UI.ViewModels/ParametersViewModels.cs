// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Common;
using Microsoft.CmdPal.Common.Messages;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// View models for parameters. This file has both the viewmodels for all the
/// different run types, and the page view model.
/// </summary>
#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

/// <summary>
/// Base class for all parameter run view models. This includes both labels and
/// parameters that accept values.
/// </summary>
public abstract partial class ParameterRunViewModel : ExtensionObjectViewModel
{
    private ExtensionObject<IParameterRun> _model;

    internal InitializedState Initialized { get; set; } = InitializedState.Uninitialized;

    protected bool IsInitialized => IsInErrorState || Initialized.HasFlag(InitializedState.Initialized);

    public bool IsInErrorState => Initialized.HasFlag(InitializedState.Error);

    internal ParameterRunViewModel(IParameterRun model, WeakReference<IPageContext> context)
        : base(context)
    {
        _model = new(model);
    }

    public override void InitializeProperties()
    {
        if (IsInitialized)
        {
            return;
        }

        var model = _model.Unsafe;
        if (model == null)
        {
            return;
        }

        model.PropChanged += Model_PropChanged;
    }

    private void Model_PropChanged(object sender, IPropChangedEventArgs args)
    {
        try
        {
            CoreLogger.LogDebug($"[{GetType().Name}] PropChanged: {args.PropertyName}");
            FetchProperty(args.PropertyName);
        }
        catch (Exception ex)
        {
            ShowException(ex);
        }
    }

    protected virtual void FetchProperty(string propertyName)
    {
        // Override in derived classes
    }
}

/// <summary>
/// View model for label runs. This is a non-interactive run that just displays
/// text.
/// </summary>
public partial class LabelRunViewModel : ParameterRunViewModel
{
    private ExtensionObject<ILabelRun> _model;

    public string Text { get; set; } = string.Empty;

    public LabelRunViewModel(ILabelRun labelRun, WeakReference<IPageContext> context)
        : base(labelRun, context)
    {
        _model = new(labelRun);
    }

    public override void InitializeProperties()
    {
        base.InitializeProperties();

        var labelRun = _model.Unsafe;
        if (labelRun == null)
        {
            return;
        }

        Text = labelRun.Text;
        UpdateProperty(nameof(Text));

        Initialized = InitializedState.Initialized;
    }

    protected override void FetchProperty(string propertyName)
    {
        var model = this._model.Unsafe;
        if (model is null)
        {
            return; // throw?
        }

        switch (propertyName)
        {
            case nameof(ILabelRun.Text):
                Text = model.Text;
                break;
        }

        UpdateProperty(propertyName);
    }
}

public partial class ParameterValueRunViewModel : ParameterRunViewModel
{
    private ExtensionObject<IParameterValueRun> _model;

    public string PlaceholderText { get; protected set; } = string.Empty;

    public bool NeedsValue { get; protected set; }

    public ParameterValueRunViewModel(IParameterValueRun valueRun, WeakReference<IPageContext> context)
        : base(valueRun, context)
    {
        _model = new(valueRun);
    }

    public override void InitializeProperties()
    {
        base.InitializeProperties();

        var valueRun = _model.Unsafe;
        if (valueRun == null)
        {
            return;
        }

        PlaceholderText = valueRun.PlaceholderText;
        NeedsValue = valueRun.NeedsValue;
        UpdateProperty(nameof(PlaceholderText));
        UpdateProperty(nameof(NeedsValue));

        Initialized = InitializedState.Initialized;
    }

    protected override void FetchProperty(string propertyName)
    {
        // Don't bother with calling base class, because it is a no-op
        var model = this._model.Unsafe;
        if (model is null)
        {
            return; // throw?
        }

        switch (propertyName)
        {
            case nameof(IParameterValueRun.PlaceholderText):
                PlaceholderText = model.PlaceholderText;
                break;
            case nameof(IParameterValueRun.NeedsValue):
                NeedsValue = model.NeedsValue;
                break;
        }

        UpdateProperty(propertyName);
    }
}

public partial class StringParameterRunViewModel : ParameterValueRunViewModel
{
    private ExtensionObject<IStringParameterRun> _model;

    private string _modelText = string.Empty;

    public string TextForUI { get => _modelText; set => SetTextFromUi(value); }

    public StringParameterRunViewModel(IStringParameterRun stringRun, WeakReference<IPageContext> context)
        : base(stringRun, context)
    {
        _model = new(stringRun);
    }

    public override void InitializeProperties()
    {
        base.InitializeProperties();

        var stringRun = _model.Unsafe;
        if (stringRun == null)
        {
            return;
        }

        _modelText = stringRun.Text;
        UpdateProperty(nameof(TextForUI));
    }

    public void SetTextFromUi(string value)
    {
        if (value != _modelText)
        {
            _modelText = value;

            _ = Task.Run(() =>
            {
                var stringRun = _model.Unsafe;
                if (stringRun != null)
                {
                    stringRun.Text = value;
                }
            });
        }
    }

    protected override void FetchProperty(string propertyName)
    {
        var model = this._model.Unsafe;
        if (model is null)
        {
            return; // throw?
        }

        switch (propertyName)
        {
            case nameof(IStringParameterRun.Text):
                var newText = model.Text;
                if (newText != _modelText)
                {
                    _modelText = newText;
                    UpdateProperty(nameof(TextForUI));
                }
                else
                {
                    return;
                }

                break;
        }

        // call the base class at the end, because ParameterValueRunViewModel
        // will handle calling UpdateProperty for the property name
        base.FetchProperty(propertyName);
    }
}

public partial class CommandParameterRunViewModel : ParameterValueRunViewModel, IDisposable
{
    private ExtensionObject<ICommandParameterRun> _model;

    private ListViewModel? _listViewModel;
    private CommandViewModel? _commandViewModel;
    private AppExtensionHost _extensionHost;
    private ICommandProviderContext _providerContext;
    private IContextMenuFactory _contextMenuFactory;

    public bool IsListParameter => _listViewModel != null;

    public ListViewModel? ListViewModel => _listViewModel;

    public string DisplayText { get; set; } = string.Empty;

    public IconInfoViewModel Icon { get; set; } = new(null);

    public string ButtonLabel => !string.IsNullOrEmpty(DisplayText) ? DisplayText : string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the user is actively editing this
    /// list parameter (browsing the list to pick a new value). This is separate
    /// from NeedsValue — a param can have a value but still be in editing mode.
    /// </summary>
    public bool IsEditing { get; set; }

    /// <summary>
    /// Gets a value indicating whether the textbox (rather than the button)
    /// should be shown. True when the param still needs a value, or the user
    /// is actively re-picking.
    /// </summary>
    public bool ShowTextBox => NeedsValue || IsEditing;

    /// <summary>
    /// Enters editing mode — switches the UI from button to textbox so the
    /// user can browse the list to re-pick a value.
    /// </summary>
    public void BeginEditing()
    {
        CoreLogger.LogDebug($"[CommandParameterRunVM] BeginEditing");
        IsEditing = true;
        UpdateProperty(nameof(IsEditing), nameof(ShowTextBox));
    }

    /// <summary>
    /// Exits editing mode — switches the UI back to button if the param
    /// already has a value.
    /// </summary>
    public void CancelEditing()
    {
        CoreLogger.LogDebug($"[CommandParameterRunVM] CancelEditing (was editing: {IsEditing})");
        IsEditing = false;
        UpdateProperty(nameof(IsEditing), nameof(ShowTextBox));
    }

    /// <summary>
    /// Raised when the extension updates value-related properties (DisplayText,
    /// Icon, or NeedsValue). Used by ParametersPageViewModel to exit editing
    /// mode and advance focus.
    /// </summary>
    public event EventHandler? ValueChanged;

    public string SearchBoxText
    {
        get => GetSearchText();
        set => SetSearchText(value);
    }

    public CommandParameterRunViewModel(ICommandParameterRun commandRun, WeakReference<IPageContext> context, AppExtensionHost extensionHost, ICommandProviderContext providerContext, IContextMenuFactory contextMenuFactory)
        : base(commandRun, context)
    {
        _model = new(commandRun);
        _extensionHost = extensionHost;
        _providerContext = providerContext;
        _contextMenuFactory = contextMenuFactory;
    }

    public override void InitializeProperties()
    {
        base.InitializeProperties();

        var commandRun = _model.Unsafe;
        if (commandRun == null)
        {
            return;
        }

        DisplayText = commandRun.DisplayText;
        Icon = new(commandRun.Icon);
        if (Icon is not null)
        {
            Icon.InitializeProperties();
        }

        GetHwndMessage msg = new();
        WeakReferenceMessenger.Default.Send(msg);
        var command = commandRun.GetSelectValueCommand((ulong)msg.Hwnd);
        if (command == null)
        {
        }
        else if (command is IListPage list)
        {
            if (PageContext.TryGetTarget(out var pageContext))
            {
                _listViewModel = new ListViewModel(list, pageContext.Scheduler, _extensionHost, _providerContext, _contextMenuFactory);
                _listViewModel.InitializeProperties();
            }
        }
        else if (command is IInvokableCommand invokable)
        {
            _commandViewModel = new CommandViewModel(invokable, this.PageContext);
            _commandViewModel.InitializeProperties();
        }

        UpdateProperty(nameof(DisplayText));
        UpdateProperty(nameof(Icon));
    }

    protected override void FetchProperty(string propertyName)
    {
        var model = this._model.Unsafe;
        if (model is null)
        {
            return; // throw?
        }

        switch (propertyName)
        {
            case nameof(ICommandParameterRun.DisplayText):
                DisplayText = model.DisplayText;
                UpdateProperty(nameof(ButtonLabel));
                break;
            case nameof(ICommandParameterRun.Icon):
                Icon = new(model.Icon);
                if (Icon is not null)
                {
                    Icon.InitializeProperties();
                }

                break;
        }

        // call the base class at the end, because ParameterValueRunViewModel
        // will handle calling UpdateProperty for the property name
        base.FetchProperty(propertyName);

        // When the extension updates any value-related property, exit editing
        // mode and notify the page.
        if (propertyName is nameof(NeedsValue)
                         or nameof(ICommandParameterRun.DisplayText)
                         or nameof(ICommandParameterRun.Icon))
        {
            CoreLogger.LogDebug($"[CommandParameterRunVM] Value-related prop changed: {propertyName}, NeedsValue={NeedsValue}, IsEditing={IsEditing}, raising ValueChanged (has subscribers: {ValueChanged != null})");
            CancelEditing();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            CoreLogger.LogDebug($"[CommandParameterRunVM] FetchProperty: {propertyName} (not value-related, no ValueChanged)");
        }
    }

    private string GetSearchText()
    {
        return _listViewModel?.SearchText ?? string.Empty;
    }

    private void SetSearchText(string value)
    {
        _listViewModel?.SearchTextBox = value;
    }

    [RelayCommand]
    public void Invoke()
    {
        if (_commandViewModel == null)
        {
            return;
        }

        PerformCommandMessage m = new(this._commandViewModel.Model);
        WeakReferenceMessenger.Default.Send(m);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _listViewModel?.Dispose();
    }
}

public partial class ParametersPageViewModel : PageViewModel, IDisposable
{
    private ExtensionObject<IParametersPage> _model;

    public override bool IsInitialized
    {
        get => base.IsInitialized; protected set
        {
            base.IsInitialized = value;
            UpdateCommand();
        }
    }

    public List<ParameterRunViewModel> Items { get; set; } = [];

    public CommandItemViewModel Command { get; private set; }

    public bool ShowCommand =>
        IsInitialized &&
        IsLoading == false &&
        !NeedsAnyValues()
        ;

    private ListViewModel? _activeListViewModel;

    public ListViewModel? ActiveListViewModel
    {
        get => _activeListViewModel;
        private set
        {
            if (_activeListViewModel != value)
            {
                _activeListViewModel = value;
                UpdateProperty(nameof(ActiveListViewModel));
                UpdateProperty(nameof(HasActiveList));
            }
        }
    }

    public bool HasActiveList => _activeListViewModel != null;

    private CommandParameterRunViewModel? _activeListParam;

    public void SetActiveListParameter(CommandParameterRunViewModel? param)
    {
        CoreLogger.LogDebug($"[ParametersPageVM] SetActiveListParameter: {(param != null ? "setting" : "clearing")} (was {(_activeListParam != null ? "set" : "null")})");
        _activeListParam = param;
        ActiveListViewModel = param?.ListViewModel;
    }

    private readonly Lock _listLock = new();

    private readonly IContextMenuFactory _contextMenuFactory;

    public ParametersPageViewModel(IParametersPage model, TaskScheduler scheduler, AppExtensionHost host, ICommandProviderContext providerContext, IContextMenuFactory contextMenuFactory)
        : base(model, scheduler, host, providerContext)
    {
        _model = new(model);
        _contextMenuFactory = contextMenuFactory;
        Command = new(new(null), PageContext, _contextMenuFactory);
    }

    private void Model_ItemsChanged(object sender, IItemsChangedEventArgs args) => FetchItems();

    //// Run on background thread, from InitializeAsync
    public override void InitializeProperties()
    {
        base.InitializeProperties();

        var model = _model.Unsafe;
        if (model is null)
        {
            return; // throw?
        }

        Command = new(new(model.Command), PageContext, _contextMenuFactory);
        Command.SlowInitializeProperties();

        FetchItems();
    }

    //// Run on background thread, from InitializeAsync or Model_ItemsChanged
    private void FetchItems()
    {
        // Collect all the items into new viewmodels
        Collection<ParameterRunViewModel> newViewModels = [];
        try
        {
            var newItems = _model.Unsafe!.Parameters;
            CoreLogger.LogDebug($"Fetched {newItems.Length} objects");
            foreach (var item in newItems)
            {
                ParameterRunViewModel? itemVm = item switch
                {
                    ILabelRun labelRun => new LabelRunViewModel(labelRun, PageContext),
                    IStringParameterRun stringRun => new StringParameterRunViewModel(stringRun, PageContext),
                    ICommandParameterRun commandRun => new CommandParameterRunViewModel(commandRun, PageContext, this.ExtensionHost, this.ProviderContext, _contextMenuFactory),
                    _ => null,
                };
                var t = itemVm?.ToString() ?? "unknown";
                CoreLogger.LogDebug($"Parameter item was a {t}");
                if (itemVm != null)
                {
                    itemVm.InitializeProperties();
                    newViewModels.Add(itemVm);
                    itemVm.PropertyChanged += ItemPropertyChanged;

                    if (itemVm is CommandParameterRunViewModel cmdParamVm)
                    {
                        cmdParamVm.ValueChanged += ListParamValueChanged;
                    }
                }
                else
                {
                    CoreLogger.LogError("Unexpected parameter type");
                }
            }

            // Update the Items collection on the UI thread
            List<ParameterRunViewModel> removedItems = [];
            lock (_listLock)
            {
                // Now that we have new ViewModels for everything from the
                // extension, smartly update our list of VMs
                ListHelpers.InPlaceUpdateList(Items, newViewModels, out removedItems);

                // DO NOT ThrowIfCancellationRequested AFTER THIS! If you do,
                // you'll clean up list items that we've now transferred into
                // .Items
            }

            // If we removed items, we need to clean them up, to remove our event handlers
            foreach (var removedItem in removedItems)
            {
                removedItem.PropertyChanged -= ItemPropertyChanged;
                if (removedItem is CommandParameterRunViewModel removedCmdParam)
                {
                    removedCmdParam.ValueChanged -= ListParamValueChanged;
                }

                removedItem.SafeCleanup();
            }
        }
        catch (Exception)
        {
            // Handle exceptions (e.g., log them)
        }

        DoOnUiThread(
            () =>
            {
                CoreLogger.LogDebug($"raising parameter items changed, {Items.Count} parameters");
                OnPropertyChanged(nameof(Items)); // This _could_ be promoted to a dedicated ItemsUpdated event if needed
                UpdateCommand();

                WeakReferenceMessenger.Default.Send(new FocusSearchBoxMessage());
            });
    }

    private void UpdateCommand()
    {
        var showCommand = ShowCommand;

        CoreLogger.LogDebug($"showCommand:{showCommand}");

        UpdateProperty(nameof(ShowCommand));

        if (!showCommand || Command.Model.Unsafe is null)
        {
            return;
        }

        UpdateProperty(nameof(Command));

        DoOnUiThread(
           () =>
           {
               WeakReferenceMessenger.Default.Send<UpdateCommandBarMessage>(new(Command));
           });
    }

    private void ItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        CoreLogger.LogDebug($"[ParametersPageVM] ItemPropertyChanged: {e.PropertyName} from {sender?.GetType().Name}, _activeListParam set: {_activeListParam != null}");

        if (e.PropertyName == nameof(ParameterValueRunViewModel.NeedsValue))
        {
            // Marshal to UI thread — PropChanged events from the extension
            // arrive on a background thread, but FocusNextParameter sends a
            // message that ultimately touches UI controls.
            DoOnUiThread(() =>
            {
                // First-time pick for a list param (NeedsValue true -> false).
                if (sender is CommandParameterRunViewModel cmdParam &&
                    cmdParam == _activeListParam &&
                    !cmdParam.NeedsValue)
                {
                    CoreLogger.LogDebug($"[ParametersPageVM] First-time list param pick, clearing active list");
                    SetActiveListParameter(null);
                    FocusNextParameter(cmdParam);
                }

                UpdateCommand();
            });
        }
    }

    private void ListParamValueChanged(object? sender, EventArgs e)
    {
        CoreLogger.LogDebug($"[ParametersPageVM] ListParamValueChanged from {sender?.GetType().Name}, _activeListParam set: {_activeListParam != null}, same: {sender == _activeListParam}");

        // Marshal to UI thread — ValueChanged is raised from the extension's
        // PropChanged callback on a background thread, but FocusNextParameter
        // sends a message that ultimately calls ContainerFromItem on a UI control.
        DoOnUiThread(() =>
        {
            // The extension confirmed a value on a list param. If it's the
            // active one (whether first pick or re-pick), clear the list and
            // move focus forward.
            if (sender is CommandParameterRunViewModel cmdParam &&
                cmdParam == _activeListParam &&
                !cmdParam.NeedsValue)
            {
                CoreLogger.LogDebug($"[ParametersPageVM] Clearing active list param after value change");
                SetActiveListParameter(null);
                FocusNextParameter(cmdParam);
                UpdateCommand();
            }
            else
            {
                CoreLogger.LogDebug($"[ParametersPageVM] ListParamValueChanged: no action (NeedsValue={(sender as CommandParameterRunViewModel)?.NeedsValue}, isActive={sender == _activeListParam})");
            }
        });
    }

    private bool NeedsAnyValues()
    {
        lock (_listLock)
        {
            foreach (var item in Items)
            {
                if (item is ParameterValueRunViewModel val &&
                    val.NeedsValue)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public void TrySubmit()
    {
        if (ShowCommand)
        {
            PerformCommandMessage m = new(this.Command.Command.Model);
            WeakReferenceMessenger.Default.Send(m);
        }
    }

    public void FocusNextParameter(ParameterValueRunViewModel lastParam)
    {
        lock (_listLock)
        {
            var found = false;
            ParameterValueRunViewModel? firstWithoutValue = null;
            foreach (var param in Items)
            {
                if (param == lastParam)
                {
                    found = true;
                    continue;
                }
                else if (param is ParameterValueRunViewModel pv)
                {
                    if (found)
                    {
                        WeakReferenceMessenger.Default.Send(new FocusParamMessage(pv));
                        return;
                    }
                    else if (firstWithoutValue is null && pv.NeedsValue)
                    {
                        firstWithoutValue = pv;
                    }
                }
            }

            if (firstWithoutValue is not null)
            {
                WeakReferenceMessenger.Default.Send(new FocusParamMessage(firstWithoutValue));
            }
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    protected override void UnsafeCleanup()
    {
        base.UnsafeCleanup();

        lock (_listLock)
        {
            foreach (var item in Items)
            {
                item.SafeCleanup();
            }

            Items.Clear();
        }
    }
}

#pragma warning restore SA1649 // File name should match first type name
#pragma warning restore SA1402 // File may only contain a single type
