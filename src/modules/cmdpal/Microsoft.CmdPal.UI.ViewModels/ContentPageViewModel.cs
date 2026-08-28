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
    private readonly ContentCollectionViewModel _content;
    private readonly Lock _commandsLock = new();
    private readonly Lock _detailsLock = new();
    private int _detailsVersion;
    private bool _detailsStopped;
    private volatile CommandSnapshot _snapshot = CommandSnapshot.Empty;

    public ObservableCollection<ContentViewModel> Content => _content.Items;

    private List<IContextItemViewModel> Commands { get; } = [];

    public bool HasCommands => _snapshot.PrimaryCommand is not null;

    public DetailsViewModel? Details { get; private set; }

    [MemberNotNullWhen(true, nameof(Details))]
    public bool HasDetails => Details is not null;

    /////// ICommandBarContext ///////
    public IReadOnlyList<IContextItemViewModel> MoreCommands => _snapshot.MoreCommands;

    public bool HasMoreCommands => _snapshot.SecondaryCommand is not null;

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
        _content = new(PageContext, ViewModelFromContent);
    }

    // TODO: Does this need to hop to a _different_ thread, so that we don't block the extension while we're fetching?
    private void Model_ItemsChanged(object sender, IItemsChangedEventArgs args) => FetchContent();

    //// Run on background thread, from InitializeAsync or Model_ItemsChanged
    private void FetchContent()
    {
        try
        {
            _content.Update(_model.Unsafe!.GetContent(), focusSoleContent: true);
        }
        catch (Exception ex)
        {
            ShowException(ex, _model?.Unsafe?.Name);
            throw;
        }
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
            RefreshCommandSnapshotsUnsafe();
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
                        RefreshCommandSnapshotsUnsafe();
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
                        RefreshCommandSnapshotsUnsafe();
                    }

                    CleanupCommandViewModels(removedItems);
                }

                UpdateProperty(nameof(PrimaryCommand));
                UpdateProperty(nameof(SecondaryCommand));
                UpdateProperty(nameof(SecondaryCommandName));
                UpdateProperty(nameof(HasCommands));
                UpdateProperty(nameof(HasMoreCommands));
                UpdateProperty(nameof(CanOpenContextMenu));
                UpdateProperty(nameof(MoreCommands));
                UpdateProperty(nameof(AllCommands));
                DoOnUiThread(
                () =>
                {
                    WeakReferenceMessenger.Default.Send<UpdateCommandBarMessage>(new(this));
                });

                break;
            case nameof(Details):
                UpdateDetails();
                break;
        }

        UpdateProperty(propertyName);
    }

    private void UpdateDetails()
    {
        int request;
        lock (_detailsLock)
        {
            if (_detailsStopped)
            {
                return;
            }

            request = ++_detailsVersion;
        }

        var extensionDetails = _model.Unsafe?.Details;
        DetailsViewModel? next = extensionDetails is not null ? new(extensionDetails, PageContext) : null;
        try
        {
            // Keep the current Details until the candidate is fully initialized.
            // No extension getter or cleanup runs under _detailsLock.
            next?.InitializeProperties();

            DetailsViewModel? previous;
            DetailsViewModel? published;
            lock (_detailsLock)
            {
                if (_detailsStopped || request != _detailsVersion)
                {
                    return;
                }

                previous = Details;
                Details = published = next;
                next = null;
            }

            previous?.SafeCleanup();
            UpdateProperty(nameof(Details), nameof(HasDetails));

            DoOnUiThread(() =>
            {
                lock (_detailsLock)
                {
                    if (_detailsStopped || !ReferenceEquals(Details, published))
                    {
                        return;
                    }
                }

                if (published is not null)
                {
                    WeakReferenceMessenger.Default.Send<ShowDetailsMessage>(new(published));
                }
                else
                {
                    WeakReferenceMessenger.Default.Send<HideDetailsMessage>();
                }
            });
        }
        finally
        {
            // Failed, superseded, or stopped candidates were never handed off.
            next?.SafeCleanup();
        }
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

    private void RefreshCommandSnapshotsUnsafe()
    {
        var allCommands = (IContextItemViewModel[])[.. Commands];
        var moreCommands = allCommands.Length > 1
            ? allCommands[1..]
            : [];

        CommandContextItemViewModel? primary = null;
        CommandContextItemViewModel? secondary = null;
        foreach (var item in allCommands)
        {
            if (item is not CommandContextItemViewModel command)
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
                break;
            }
        }

        _snapshot = new(allCommands, moreCommands, primary, secondary);
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
        DetailsViewModel? details;
        lock (_detailsLock)
        {
            _detailsStopped = true;
            ++_detailsVersion;
            details = Details;
            Details = null;
        }

        base.UnsafeCleanup();
        details?.SafeCleanup();

        List<IContextItemViewModel> removedItems;
        lock (_commandsLock)
        {
            removedItems = [.. Commands];
            Commands.Clear();
            RefreshCommandSnapshotsUnsafe();
        }

        CleanupCommandViewModels(removedItems);

        _content.SafeCleanup();

        var model = _model.Unsafe;
        if (model is not null)
        {
            model.ItemsChanged -= Model_ItemsChanged;
        }
    }

    /// <summary>
    /// Immutable bundle of derived command state, published atomically via a
    /// single volatile write so readers never see a torn snapshot.
    /// </summary>
    private sealed class CommandSnapshot(
        IContextItemViewModel[] allCommands,
        IContextItemViewModel[] moreCommands,
        CommandContextItemViewModel? primaryCommand,
        CommandContextItemViewModel? secondaryCommand)
    {
        public static CommandSnapshot Empty { get; } = new([], [], null, null);

        public IContextItemViewModel[] AllCommands { get; } = allCommands;

        public IContextItemViewModel[] MoreCommands { get; } = moreCommands;

        public CommandContextItemViewModel? PrimaryCommand { get; } = primaryCommand;

        public CommandContextItemViewModel? SecondaryCommand { get; } = secondaryCommand;
    }
}
