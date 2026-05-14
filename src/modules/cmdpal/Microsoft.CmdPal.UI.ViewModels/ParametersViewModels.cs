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

    protected override void UnsafeCleanup()
    {
        base.UnsafeCleanup();

        // Unsubscribe from the extension model's PropChanged event so we
        // don't keep this view model alive for as long as the extension
        // object lives.
        var model = _model.Unsafe;
        if (model is not null)
        {
            model.PropChanged -= Model_PropChanged;
        }
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

public partial class StringParameterRunViewModel : ParameterValueRunViewModel, IDisposable
{
    // Exclusive scheduler ensures writes to the extension's Text property are
    // serialized in the order they were submitted from the UI, so rapid
    // typing can't deliver updates out of order.
    private readonly TaskFactory _writeTaskFactory = new(new ConcurrentExclusiveSchedulerPair().ExclusiveScheduler);

    private ExtensionObject<IStringParameterRun> _model;

    private string _modelText = string.Empty;

    // For cancelling in-flight writes when a newer value arrives.
    private CancellationTokenSource? _writeCancellationTokenSource;

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
        if (value == _modelText)
        {
            return;
        }

        _modelText = value;

        // Cancel any pending write that hasn't started yet, so we don't push
        // stale values to the extension.
        CancelAndDisposeTokenSource(ref _writeCancellationTokenSource);
        var writeCts = _writeCancellationTokenSource = new CancellationTokenSource();
        var writeToken = writeCts.Token;

        // Hop off to an exclusive scheduler background thread to update the
        // extension. The exclusive scheduler ensures writes are serialized
        // and in-order (mirroring ListViewModel.OnSearchTextBoxUpdated).
        _ = _writeTaskFactory.StartNew(
            () =>
            {
                if (writeToken.IsCancellationRequested)
                {
                    return;
                }

                try
                {
                    var stringRun = _model.Unsafe;
                    if (stringRun != null)
                    {
                        stringRun.Text = value;
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    ShowException(ex);
                }
            },
            writeToken,
            TaskCreationOptions.None,
            _writeTaskFactory.Scheduler!);
    }

    private static void CancelAndDisposeTokenSource(ref CancellationTokenSource? tokenSource)
    {
        var tokenSourceToDispose = Interlocked.Exchange(ref tokenSource, null);
        if (tokenSourceToDispose is null)
        {
            return;
        }

        tokenSourceToDispose.Cancel();
        tokenSourceToDispose.Dispose();
    }

    protected override void UnsafeCleanup()
    {
        base.UnsafeCleanup();

        CancelAndDisposeTokenSource(ref _writeCancellationTokenSource);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        SafeCleanup();
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

    public string DisplayText { get; set; } = string.Empty;

    public IconInfoViewModel Icon { get; set; } = new(null);

    public string ButtonLabel => !string.IsNullOrEmpty(DisplayText) ? DisplayText : string.Empty;

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
        if (command is IListPage list)
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

    protected override void UnsafeCleanup()
    {
        base.UnsafeCleanup();

        _listViewModel?.Dispose();
        _listViewModel = null;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        SafeCleanup();
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
                removedItem.SafeCleanup();
            }
        }
        catch (Exception ex)
        {
            CoreLogger.LogError($"Error fetching parameter items: {ex.Message}");
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
        if (e.PropertyName == nameof(ParameterValueRunViewModel.NeedsValue))
        {
            UpdateCommand();
        }
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
        SafeCleanup();
    }

    protected override void UnsafeCleanup()
    {
        base.UnsafeCleanup();

        lock (_listLock)
        {
            foreach (var item in Items)
            {
                item.PropertyChanged -= ItemPropertyChanged;
                item.SafeCleanup();
            }

            Items.Clear();
        }
    }
}

#pragma warning restore SA1649 // File name should match first type name
#pragma warning restore SA1402 // File may only contain a single type
