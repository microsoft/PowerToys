// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ContentPageViewModel : PageViewModel, ICommandBarContext
{
    private readonly ExtensionObject<IContentPage> _model;
    private readonly Lock _commandsLock = new();
    private volatile CommandContextSnapshot _snapshot = CommandContextSnapshot.Empty;

    [ObservableProperty]
    public partial ObservableCollection<ContentViewModel> Content { get; set; } = [];

    private List<IContextItemViewModel> Commands { get; } = [];

    public bool HasCommands => _snapshot.PrimaryCommand is not null;

    public DetailsViewModel? Details { get; private set; }

    [MemberNotNullWhen(true, nameof(Details))]
    public bool HasDetails => Details is not null;

    /////// ICommandBarContext ///////
    public bool HasOverflowCommands => _snapshot.HasOverflowCommands;

    public bool CanOpenContextMenu => _snapshot.AllCommands.Any(item => item is CommandItemViewModel command && command.ShouldBeVisible);

    public string SecondaryCommandName => _snapshot.SecondaryCommand?.Name ?? string.Empty;

    public CommandItemViewModel? PrimaryCommand => _snapshot.PrimaryCommand;

    public CommandItemViewModel? SecondaryCommand => _snapshot.SecondaryCommand;

    public IReadOnlyList<IContextItemViewModel> AllCommands => _snapshot.AllCommands;

    // Remember - "observable" properties from the model (via PropChanged)
    // cannot be marked [ObservableProperty]
    public ContentPageViewModel(IContentPage model, TaskScheduler scheduler, AppExtensionHost host, ICommandProviderContext providerContext)
        : base(model, scheduler, host, providerContext)
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

        var commands = BuildCommandViewModels(model.Commands);
        InitializeCommandViewModels(commands, static contextItem => contextItem.InitializeProperties());

        lock (_commandsLock)
        {
            ListHelpers.InPlaceUpdateList(Commands, commands);
            RefreshCommandSnapshotsUnsafe(contextItemsChanged: true);
        }

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
                    var newContextMenu = BuildCommandViewModels(more);
                    InitializeCommandViewModels(newContextMenu, static contextItem => contextItem.SlowInitializeProperties());

                    List<IContextItemViewModel> removedItems;
                    lock (_commandsLock)
                    {
                        ListHelpers.InPlaceUpdateList(Commands, newContextMenu, out removedItems);
                        RefreshCommandSnapshotsUnsafe(contextItemsChanged: true);
                    }

                    CleanupCommandViewModels(removedItems);
                }
                else
                {
                    List<IContextItemViewModel> removedItems;
                    lock (_commandsLock)
                    {
                        removedItems = [.. Commands];
                        Commands.Clear();
                        RefreshCommandSnapshotsUnsafe(contextItemsChanged: true);
                    }

                    CleanupCommandViewModels(removedItems);
                }

                NotifyCommandsChanged();
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

    private List<IContextItemViewModel> BuildCommandViewModels(IContextItem[]? items)
    {
        if (items is null)
        {
            return [];
        }

        return items
            .Select<IContextItem, IContextItemViewModel>(item =>
            {
                if (item is ICommandContextItem contextItem)
                {
                    return new CommandContextItemViewModel(contextItem, PageContext);
                }

                return new SeparatorViewModel();
            })
            .ToList();
    }

    private static void InitializeCommandViewModels(IEnumerable<IContextItemViewModel> commands, Action<CommandContextItemViewModel> initialize)
    {
        foreach (var contextItem in commands.OfType<CommandContextItemViewModel>())
        {
            initialize(contextItem);
        }
    }

    private static void CleanupCommandViewModels(IEnumerable<IContextItemViewModel> commands)
    {
        foreach (var contextItem in commands.OfType<CommandContextItemViewModel>())
        {
            contextItem.SafeCleanup();
        }
    }

    private void ContextItem_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(CommandItemViewModel.Name) || sender is not CommandContextItemViewModel command)
        {
            return;
        }

        bool rolesChanged;
        lock (_commandsLock)
        {
            // A queued notification may arrive after the entry was replaced.
            if (!Commands.Contains(command))
            {
                return;
            }

            var previousSnapshot = _snapshot;
            RefreshCommandSnapshotsUnsafe();
            rolesChanged = !ReferenceEquals(previousSnapshot, _snapshot);
        }

        if (rolesChanged)
        {
            NotifyCommandsChanged();
        }
        else if (ReferenceEquals(command, SecondaryCommand))
        {
            UpdateProperty(nameof(SecondaryCommandName));
        }
    }

    private void NotifyCommandsChanged() =>
        UpdateProperty(
            nameof(PrimaryCommand),
            nameof(SecondaryCommand),
            nameof(SecondaryCommandName),
            nameof(HasCommands),
            nameof(HasOverflowCommands),
            nameof(CanOpenContextMenu),
            nameof(AllCommands));

    private void RefreshCommandSnapshotsUnsafe(bool contextItemsChanged = false)
    {
        if (contextItemsChanged)
        {
            foreach (var command in _snapshot.AllCommands.OfType<CommandContextItemViewModel>())
            {
                command.PropertyChangedBackground -= ContextItem_PropertyChanged;
            }

            foreach (var command in Commands.OfType<CommandContextItemViewModel>())
            {
                command.PropertyChangedBackground += ContextItem_PropertyChanged;
            }
        }

        IContextItemViewModel[] allCommands = contextItemsChanged ? [.. Commands] : _snapshot.AllCommands;
        var hasOverflowCommands = false;

        CommandContextItemViewModel? primary = null;
        CommandContextItemViewModel? secondary = null;
        for (var i = 0; i < allCommands.Length; i++)
        {
            if (allCommands[i] is not CommandContextItemViewModel command || !command.ShouldBeVisible)
            {
                continue;
            }

            if (primary is null)
            {
                primary = command;
            }
            else if (secondary is null)
            {
                secondary = command;
            }
            else
            {
                hasOverflowCommands = true;
                break;
            }
        }

        if (ReferenceEquals(allCommands, _snapshot.AllCommands) &&
            ReferenceEquals(primary, _snapshot.PrimaryCommand) &&
            ReferenceEquals(secondary, _snapshot.SecondaryCommand) &&
            hasOverflowCommands == _snapshot.HasOverflowCommands)
        {
            return;
        }

        _snapshot = new(allCommands, primary, secondary, hasOverflowCommands, hasSubmenu: false);
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

        List<IContextItemViewModel> removedItems;
        lock (_commandsLock)
        {
            removedItems = [.. Commands];
            Commands.Clear();
            RefreshCommandSnapshotsUnsafe(contextItemsChanged: true);
        }

        CleanupCommandViewModels(removedItems);

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
