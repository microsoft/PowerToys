// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Models;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ContentPageViewModel : PageViewModel
{
    private readonly ExtensionObject<IContentPage> _model;

    [ObservableProperty]
    public partial ObservableCollection<ContentViewModel> Content { get; set; } = [];

    public List<CommandContextItemViewModel> Commands { get; private set; } = [];

    public bool HasCommands => Commands.Count > 0;

    public DetailsViewModel? Details { get; private set; }

    [MemberNotNullWhen(true, nameof(Details))]
    public bool HasDetails => Details != null;

    // Remember - "observable" properties from the model (via PropChanged)
    // cannot be marked [ObservableProperty]
    public ContentPageViewModel(IContentPage model, TaskScheduler scheduler, CommandPaletteHost host)
        : base(model, scheduler, host)
    {
        _model = new(model);
    }

    // TODO: Does this need to hop to a _different_ thread, so that we don't block the extension while we're fetching?
    private void Model_ItemsChanged(object sender, ItemsChangedEventArgs args) => FetchContent();

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
                if (viewModel != null)
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

        // Now, back to a UI thread to update the observable collection
        Task.Factory.StartNew(
        () =>
        {
            ListHelpers.InPlaceUpdateList(Content, newContent);
        },
        CancellationToken.None,
        TaskCreationOptions.None,
        PageContext.Scheduler);
    }

    public static ContentViewModel? ViewModelFromContent(IContent content, IPageContext context)
    {
        ContentViewModel? viewModel = content switch
        {
            IFormContent form => new ContentFormViewModel(form, context),
            IMarkdownContent markdown => new ContentMarkdownViewModel(markdown, context),
            ITreeContent tree => new ContentTreeViewModel(tree, context),
            _ => null,
        };
        return viewModel;
    }

    public override void InitializeProperties()
    {
        base.InitializeProperties();

        var model = _model.Unsafe;
        if (model == null)
        {
            return; // throw?
        }

        Commands = model.Commands
            .Where(contextItem => contextItem is ICommandContextItem)
            .Select(contextItem => (contextItem as ICommandContextItem)!)
            .Select(contextItem => new CommandContextItemViewModel(contextItem, PageContext))
            .ToList();

        var extensionDetails = model.Details;
        if (extensionDetails != null)
        {
            Details = new(extensionDetails, PageContext);
            Details.InitializeProperties();
        }

        UpdateDetails();

        FetchContent();
        model.ItemsChanged += Model_ItemsChanged;
    }

    protected override void FetchProperty(string propertyName)
    {
        base.FetchProperty(propertyName);

        var model = this._model.Unsafe;
        if (model == null)
        {
            return; // throw?
        }

        switch (propertyName)
        {
            // case nameof(Commands):
            //     TODO GH #360 - make MoreCommands observable
            //     this.ShowDetails = model.ShowDetails;
            //     break;
            case nameof(Details):
                var extensionDetails = model.Details;
                Details = extensionDetails != null ? new(extensionDetails, PageContext) : null;
                UpdateDetails();
                break;
        }

        UpdateProperty(propertyName);
    }

    private void UpdateDetails()
    {
        UpdateProperty(nameof(Details));
        UpdateProperty(nameof(HasDetails));

        Task.Factory.StartNew(
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
            },
            CancellationToken.None,
            TaskCreationOptions.None,
            PageContext.Scheduler);
    }
}
