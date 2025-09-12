// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Messaging;
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
    internal InitializedState Initialized { get; set; } = InitializedState.Uninitialized;

    protected bool IsInitialized => IsInErrorState || Initialized.HasFlag(InitializedState.Initialized);

    public bool IsInErrorState => Initialized.HasFlag(InitializedState.Error);

    internal ParameterRunViewModel(WeakReference<IPageContext> context)
        : base(context)
    {
    }
}

public partial class LabelRunViewModel : ParameterRunViewModel
{
    private ExtensionObject<ILabelRun> _model;

    public string Text { get; set; } = string.Empty;

    public LabelRunViewModel(ILabelRun labelRun, WeakReference<IPageContext> context)
        : base(context)
    {
        _model = new(labelRun);
    }

    public override void InitializeProperties()
    {
        if (IsInitialized)
        {
            return;
        }

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

    public string PlaceholderText { get; set; } = string.Empty;

    public bool NeedsValue { get; set; }

    public ParameterValueRunViewModel(IParameterValueRun valueRun, WeakReference<IPageContext> context)
        : base(context)
    {
        _model = new(valueRun);
    }

    public override void InitializeProperties()
    {
        if (IsInitialized)
        {
            return;
        }

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
}

public partial class StringParameterRunViewModel : ParameterValueRunViewModel
{
    private ExtensionObject<IStringParameterRun> _model;

    private string _modelText = string.Empty;

    public string Text { get => _modelText; set => SetText(value); }

    public StringParameterRunViewModel(IStringParameterRun stringRun, WeakReference<IPageContext> context)
        : base(stringRun, context)
    {
        _model = new(stringRun);
    }

    public override void InitializeProperties()
    {
        if (IsInitialized)
        {
            return;
        }

        base.InitializeProperties();
        var stringRun = _model.Unsafe;
        if (stringRun == null)
        {
            return;
        }

        Text = stringRun.Text;
        UpdateProperty(nameof(Text));
    }

    private void SetText(string value)
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
}

public partial class CommandParameterRunViewModel : ParameterValueRunViewModel
{
    private ExtensionObject<ICommandParameterRun> _model;

    public string DisplayText { get; set; } = string.Empty;

    public IconInfoViewModel Icon { get; set; } = new(null);

    public CommandParameterRunViewModel(ICommandParameterRun commandRun, WeakReference<IPageContext> context)
        : base(commandRun, context)
    {
        _model = new(commandRun);
    }

    public override void InitializeProperties()
    {
        if (IsInitialized)
        {
            return;
        }

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

        UpdateProperty(nameof(DisplayText));
        UpdateProperty(nameof(Icon));
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

        // FilteredItems.Count == 0 &&
        // (!_isFetching) &&
        IsLoading == false;

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
                    ICommandParameterRun commandRun => new CommandParameterRunViewModel(commandRun, PageContext),
                    _ => null,
                };
                if (itemVm != null)
                {
                    itemVm.InitializeProperties();
                    newViewModels.Add(itemVm);
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
