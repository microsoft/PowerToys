// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Core.ViewModels.Messages;
using Microsoft.CmdPal.Core.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Core.ViewModels;

public abstract partial class ContentPageViewModel : PageViewModel, ICommandBarContext
{
    private readonly ExtensionObject<IContentPage> _model;

    [ObservableProperty]
    public partial ObservableCollection<ContentViewModel> Content { get; set; } = [];

    public List<IContextItemViewModel> Commands { get; private set; } = [];

    public bool HasCommands => ActualCommands.Count > 0;

    public DetailsViewModel? Details { get; private set; }

    [MemberNotNullWhen(true, nameof(Details))]
    public bool HasDetails => Details is not null;

    /////// ICommandBarContext ///////
    public IEnumerable<IContextItemViewModel> MoreCommands => Commands.Skip(1);

    private List<CommandContextItemViewModel> ActualCommands => Commands.OfType<CommandContextItemViewModel>().ToList();

    public bool HasMoreCommands => ActualCommands.Count > 1;

    public string SecondaryCommandName => SecondaryCommand?.Name ?? string.Empty;

    public CommandItemViewModel? PrimaryCommand => HasCommands ? ActualCommands[0] : null;

    public CommandItemViewModel? SecondaryCommand => HasMoreCommands ? ActualCommands[1] : null;

    public List<IContextItemViewModel> AllCommands => Commands;

    // Remember - "observable" properties from the model (via PropChanged)
    // cannot be marked [ObservableProperty]
    public ContentPageViewModel(IContentPage model, TaskScheduler scheduler, AppExtensionHost host)
        : base(model, scheduler, host)
    {
        _model = new(model);
    }

    // TODO: Does this need to hop to a _different_ thread, so that we don't block the extension while we're fetching?
    private void Model_ItemsChanged(object sender, IItemsChangedEventArgs args) => FetchContent();

    //// Run on background thread, from InitializeAsync or Model_ItemsChanged
    private void FetchContent()
    {
        List<ContentViewModel> newContent = [];
        try
        {
            var newItems = _model.Unsafe!.GetContent();

            foreach (var item in newItems)
            {
                var viewModel = ViewModelFromContent(item, PageContext);
                if (viewModel is not null)
                {
                    viewModel.InitializeProperties();
                    newContent.Add(viewModel);
                }
            }
        }
        catch (Exception ex)
        {
            ShowException(ex, _model?.Unsafe?.Name);
            throw;
        }

        var oneContent = newContent.Count == 1;
        newContent.ForEach(c => c.OnlyControlOnPage = oneContent);

        // Now, back to a UI thread to update the observable collection
        DoOnUiThread(
        () =>
        {
            ListHelpers.InPlaceUpdateList(Content, newContent);
        });
    }

    public virtual ContentViewModel? ViewModelFromContent(IContent content, WeakReference<IPageContext> context)
    {
        // The core ContentPageViewModel doesn't actually handle any content,
        // so we just return null here.
        // The real content is handled by the derived class CommandPaletteContentPageViewModel
        return null;
    }

    public override void InitializeProperties()
    {
        base.InitializeProperties();

        var model = _model.Unsafe;
        if (model is null)
        {
            return; // throw?
        }

        Commands = model.Commands
                .ToList()
                .Select<IContextItem, IContextItemViewModel>(item =>
                {
                    if (item is ICommandContextItem contextItem)
                    {
                        return new CommandContextItemViewModel(contextItem, PageContext);
                    }
                    else
                    {
                        return new SeparatorContextItemViewModel();
                    }
                })
                .ToList();

        Commands
            .OfType<CommandContextItemViewModel>()
            .ToList()
            .ForEach(contextItem =>
            {
                contextItem.InitializeProperties();
            });

        var extensionDetails = model.Details;
        if (extensionDetails is not null)
        {
            Details = new(extensionDetails, PageContext);
            Details.InitializeProperties();
        }

        UpdateDetails();

        FetchContent();
        model.ItemsChanged += Model_ItemsChanged;

        DoOnUiThread(
        () =>
        {
            WeakReferenceMessenger.Default.Send<UpdateCommandBarMessage>(new(this));
        });
    }

    protected override void FetchProperty(string propertyName)
    {
        base.FetchProperty(propertyName);

        var model = this._model.Unsafe;
        if (model is null)
        {
            return; // throw?
        }

        switch (propertyName)
        {
            case nameof(Commands):

                var more = model.Commands;
                if (more is not null)
                {
                    var newContextMenu = more
                            .ToList()
                            .Select(item =>
                            {
                                if (item is ICommandContextItem contextItem)
                                {
                                    return new CommandContextItemViewModel(contextItem, PageContext) as IContextItemViewModel;
                                }
                                else
                                {
                                    return new SeparatorContextItemViewModel();
                                }
                            })
                            .ToList();

                    lock (Commands)
                    {
                        ListHelpers.InPlaceUpdateList(Commands, newContextMenu);
                    }

                    Commands
                        .OfType<CommandContextItemViewModel>()
                        .ToList()
                        .ForEach(contextItem =>
                        {
                            contextItem.SlowInitializeProperties();
                        });
                }
                else
                {
                    Commands.Clear();
                }

                UpdateProperty(nameof(PrimaryCommand));
                UpdateProperty(nameof(SecondaryCommand));
                UpdateProperty(nameof(SecondaryCommandName));
                UpdateProperty(nameof(HasCommands));
                UpdateProperty(nameof(HasMoreCommands));
                UpdateProperty(nameof(AllCommands));
                DoOnUiThread(
                () =>
                {
                    WeakReferenceMessenger.Default.Send<UpdateCommandBarMessage>(new(this));
                });

                break;
            case nameof(Details):
                var extensionDetails = model.Details;
                Details = extensionDetails is not null ? new(extensionDetails, PageContext) : null;
                UpdateDetails();
                break;
        }

        UpdateProperty(propertyName);
    }

    private void UpdateDetails()
    {
        UpdateProperty(nameof(Details));
        UpdateProperty(nameof(HasDetails));

        DoOnUiThread(
            () =>
            {
                if (HasDetails)
                {
                    WeakReferenceMessenger.Default.Send<ShowDetailsMessage>(new(Details));
                }
                else
                {
                    WeakReferenceMessenger.Default.Send<HideDetailsMessage>();
                }
            });
    }

    // InvokeItemCommand is what this will be in Xaml due to source generator
    // this comes in on Enter keypresses in the SearchBox
    [RelayCommand]
    private void InvokePrimaryCommand(ContentPageViewModel page)
    {
        if (PrimaryCommand is not null)
        {
            WeakReferenceMessenger.Default.Send<PerformCommandMessage>(new(PrimaryCommand.Command.Model, PrimaryCommand.Model));
        }
    }

    // this comes in on Ctrl+Enter keypresses in the SearchBox
    [RelayCommand]
    private void InvokeSecondaryCommand(ContentPageViewModel page)
    {
        if (SecondaryCommand is not null)
        {
            WeakReferenceMessenger.Default.Send<PerformCommandMessage>(new(SecondaryCommand.Command.Model, SecondaryCommand.Model));
        }
    }

    protected override void UnsafeCleanup()
    {
        base.UnsafeCleanup();

        Details?.SafeCleanup();

        Commands
            .OfType<CommandContextItemViewModel>()
            .ToList()
            .ForEach(item => item.SafeCleanup());

        Commands.Clear();

        foreach (var item in Content)
        {
            item.SafeCleanup();
        }

        Content.Clear();

        var model = _model.Unsafe;
        if (model is not null)
        {
            model.ItemsChanged -= Model_ItemsChanged;
        }
    }
}
