// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Common.Messages;
using Microsoft.CmdPal.Core.ViewModels.Messages;
using Microsoft.CmdPal.Core.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;

namespace Microsoft.CmdPal.Core.ViewModels;

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

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
}

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

        // if (IsInitialized)
        // {
        //    return;
        // }
        var labelRun = _model.Unsafe;
        if (labelRun == null)
        {
            return;
        }

        Text = labelRun.Text;
        UpdateProperty(nameof(Text));

        Initialized = InitializedState.Initialized;
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

        // if (IsInitialized)
        // {
        //    return;
        // }
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

        // if (IsInitialized)
        // {
        //    return;
        // }
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

    public string DisplayText { get; set; } = string.Empty;

    public IconInfoViewModel Icon { get; set; } = new(null);

    public string ButtonLabel => !string.IsNullOrEmpty(DisplayText) ? DisplayText : string.Empty;

    public string SearchBoxText
    {
        get => GetSearchText();
        set => SetSearchText(value);
    }

    public CommandParameterRunViewModel(ICommandParameterRun commandRun, WeakReference<IPageContext> context, AppExtensionHost extensionHost)
        : base(commandRun, context)
    {
        _model = new(commandRun);
        _extensionHost = extensionHost;
    }

    public override void InitializeProperties()
    {
        base.InitializeProperties();

        // if (IsInitialized)
        // {
        //    return;
        // }
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
                _listViewModel = new ListViewModel(list, pageContext.Scheduler, _extensionHost);
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

    public ObservableCollection<ParameterRunViewModel> Items { get; set; } = [];

    public CommandItemViewModel Command { get; private set; }

    public bool ShowCommand =>
        IsInitialized &&
        IsLoading == false &&
        !NeedsAnyValues()

        // FilteredItems.Count == 0 &&
        // (!_isFetching) &&
        ;

    private readonly Lock _listLock = new();

    public event TypedEventHandler<ParametersPageViewModel, object>? ItemsUpdated;

    public ParametersPageViewModel(IParametersPage model, TaskScheduler scheduler, AppExtensionHost host)
        : base(model, scheduler, host)
    {
        _model = new(model);
        Command = new(new(null), PageContext);
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

        Command = new(new(model.Command), PageContext);
        Command.SlowInitializeProperties();

        FetchItems();

        // model.ItemsChanged += Model_ItemsChanged; // TODO!
    }

    //// Run on background thread, from InitializeAsync or Model_ItemsChanged
    private void FetchItems()
    {
        // Collect all the items into new viewmodels
        Collection<ParameterRunViewModel> newViewModels = [];
        try
        {
            var newItems = _model.Unsafe!.Parameters;
            foreach (var item in newItems)
            {
                ParameterRunViewModel? itemVm = item switch
                {
                    ILabelRun labelRun => new LabelRunViewModel(labelRun, PageContext),
                    IStringParameterRun stringRun => new StringParameterRunViewModel(stringRun, PageContext),
                    ICommandParameterRun commandRun => new CommandParameterRunViewModel(commandRun, PageContext, this.ExtensionHost),
                    _ => null,
                };
                if (itemVm != null)
                {
                    itemVm.InitializeProperties();
                    newViewModels.Add(itemVm);
                    itemVm.PropertyChanged += ItemPropertyChanged;
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
        catch (Exception)
        {
            // Handle exceptions (e.g., log them)
        }

        DoOnUiThread(
            () =>
            {
                ItemsUpdated?.Invoke(this, EventArgs.Empty);
                OnPropertyChanged(nameof(Items)); // TODO! hack
                UpdateCommand();

                WeakReferenceMessenger.Default.Send(new FocusSearchBoxMessage());
            });
    }

    private void UpdateCommand()
    {
        UpdateProperty(nameof(ShowCommand));
        if (!ShowCommand || Command.Model.Unsafe is null)
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

        // TODO!

        // _cancellationTokenSource?.Cancel();
        // _cancellationTokenSource?.Dispose();
        // _cancellationTokenSource = null;

        // _fetchItemsCancellationTokenSource?.Cancel();
        // _fetchItemsCancellationTokenSource?.Dispose();
        // _fetchItemsCancellationTokenSource = null;
    }

    protected override void UnsafeCleanup()
    {
        base.UnsafeCleanup();

        // _cancellationTokenSource?.Cancel();
        // _fetchItemsCancellationTokenSource?.Cancel();
        var model = _model.Unsafe;
        if (model is not null)
        {
            // model.ItemsChanged -= Model_ItemsChanged;
        }

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
