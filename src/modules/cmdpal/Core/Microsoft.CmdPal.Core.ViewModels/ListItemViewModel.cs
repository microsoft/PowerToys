// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.CmdPal.Core.ViewModels.Commands;
using Microsoft.CmdPal.Core.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Core.ViewModels;

public partial class ListItemViewModel : CommandItemViewModel
{
    public new ExtensionObject<IListItem> Model { get; }

    public List<TagViewModel>? Tags { get; set; }

    // Remember - "observable" properties from the model (via PropChanged)
    // cannot be marked [ObservableProperty]
    public bool HasTags => (Tags?.Count ?? 0) > 0;

    public string TextToSuggest { get; private set; } = string.Empty;

    public string Section { get; private set; } = string.Empty;

    public DetailsViewModel? Details { get; private set; }

    [MemberNotNullWhen(true, nameof(Details))]
    public bool HasDetails => Details is not null;

    public string AccessibleName { get; private set; } = string.Empty;

    public bool ShowTitle { get; private set; }

    public bool ShowSubtitle { get; private set; }

    public bool LayoutShowsTitle
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                UpdateShowsTitle();
            }
        }
    }

    public bool LayoutShowsSubtitle
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                UpdateShowsSubtitle();
            }
        }
    }

    public ListItemViewModel(IListItem model, WeakReference<IPageContext> context)
        : base(new(model), context)
    {
        Model = new ExtensionObject<IListItem>(model);
    }

    public override void InitializeProperties()
    {
        if (IsInitialized)
        {
            return;
        }

        // This sets IsInitialized = true
        base.InitializeProperties();

        var li = Model.Unsafe;
        if (li is null)
        {
            return; // throw?
        }

        UpdateTags(li.Tags);

        Section = li.Section ?? string.Empty;

        UpdateProperty(nameof(Section));

        UpdateAccessibleName();
    }

    public override void SlowInitializeProperties()
    {
        base.SlowInitializeProperties();
        var model = Model.Unsafe;
        if (model is null)
        {
            return;
        }

        var extensionDetails = model.Details;
        if (extensionDetails is not null)
        {
            Details = new(extensionDetails, PageContext);
            Details.InitializeProperties();
            UpdateProperty(nameof(Details));
            UpdateProperty(nameof(HasDetails));
        }

        AddShowDetailsCommands();

        TextToSuggest = model.TextToSuggest;
        UpdateProperty(nameof(TextToSuggest));
    }

    protected override void FetchProperty(string propertyName)
    {
        base.FetchProperty(propertyName);

        var model = this.Model.Unsafe;
        if (model is null)
        {
            return; // throw?
        }

        switch (propertyName)
        {
            case nameof(model.Tags):
                UpdateTags(model.Tags);
                break;
            case nameof(model.TextToSuggest):
                TextToSuggest = model.TextToSuggest ?? string.Empty;
                UpdateProperty(nameof(TextToSuggest));
                break;
            case nameof(model.Section):
                Section = model.Section ?? string.Empty;
                UpdateProperty(nameof(Section));
                break;
            case nameof(model.Details):
                var extensionDetails = model.Details;
                Details = extensionDetails is not null ? new(extensionDetails, PageContext) : null;
                Details?.InitializeProperties();
                UpdateProperty(nameof(Details));
                UpdateProperty(nameof(HasDetails));
                UpdateShowDetailsCommand();
                break;
            case nameof(model.MoreCommands):
                UpdateProperty(nameof(MoreCommands));
                AddShowDetailsCommands();
                break;
            case nameof(model.Title):
                UpdateProperty(nameof(Title));
                UpdateShowsTitle();
                UpdateAccessibleName();
                break;
            case nameof(model.Subtitle):
                UpdateProperty(nameof(Subtitle));
                UpdateShowsSubtitle();
                UpdateAccessibleName();
                break;
            default:
                UpdateProperty(propertyName);
                break;
        }
    }

    // TODO: Do we want filters to match descriptions and other properties? Tags, etc... Yes?
    // TODO: Do we want to save off the score here so we can sort by it in our ListViewModel?
    public override string ToString() => $"{Name} ListItemViewModel";

    public override bool Equals(object? obj) => obj is ListItemViewModel vm && vm.Model.Equals(this.Model);

    public override int GetHashCode() => Model.GetHashCode();

    private void AddShowDetailsCommands()
    {
        // If the parent page has ShowDetails = false and we have details,
        // then we should add a show details action in the context menu.
        if (HasDetails &&
            PageContext.TryGetTarget(out var pageContext) &&
            pageContext is ListViewModel listViewModel &&
            !listViewModel.ShowDetails)
        {
            // Check if "Show Details" action already exists to prevent duplicates
            if (!MoreCommands.Any(cmd => cmd is CommandContextItemViewModel contextItemViewModel &&
                                        contextItemViewModel.Command.Id == ShowDetailsCommand.ShowDetailsCommandId))
            {
                // Create the view model for the show details command
                var showDetailsCommand = new ShowDetailsCommand(Details);
                var showDetailsContextItem = new CommandContextItem(showDetailsCommand);
                var showDetailsContextItemViewModel = new CommandContextItemViewModel(showDetailsContextItem, PageContext);
                showDetailsContextItemViewModel.SlowInitializeProperties();
                MoreCommands.Add(showDetailsContextItemViewModel);
            }

            UpdateProperty(nameof(MoreCommands));
            UpdateProperty(nameof(AllCommands));
        }
    }

    // This method is called when the details change to make sure we
    // have the latest details in the show details command.
    private void UpdateShowDetailsCommand()
    {
        // If the parent page has ShowDetails = false and we have details,
        // then we should add a show details action in the context menu.
        if (HasDetails &&
            PageContext.TryGetTarget(out var pageContext) &&
            pageContext is ListViewModel listViewModel &&
            !listViewModel.ShowDetails)
        {
            var existingCommand = MoreCommands.FirstOrDefault(cmd =>
                                        cmd is CommandContextItemViewModel contextItemViewModel &&
                                        contextItemViewModel.Command.Id == ShowDetailsCommand.ShowDetailsCommandId);

            // If the command already exists, remove it to update with the new details
            if (existingCommand is not null)
            {
                MoreCommands.Remove(existingCommand);
            }

            // Create the view model for the show details command
            var showDetailsCommand = new ShowDetailsCommand(Details);
            var showDetailsContextItem = new CommandContextItem(showDetailsCommand);
            var showDetailsContextItemViewModel = new CommandContextItemViewModel(showDetailsContextItem, PageContext);
            showDetailsContextItemViewModel.SlowInitializeProperties();
            MoreCommands.Add(showDetailsContextItemViewModel);

            UpdateProperty(nameof(MoreCommands));
            UpdateProperty(nameof(AllCommands));
        }
    }

    private void UpdateTags(ITag[]? newTagsFromModel)
    {
        var newTags = newTagsFromModel?.Select(t =>
        {
            var vm = new TagViewModel(t, PageContext);
            vm.InitializeProperties();
            return vm;
        })
            .ToList() ?? [];

        DoOnUiThread(
            () =>
            {
                // Tags being an ObservableCollection instead of a List lead to
                // many COM exception issues.
                Tags = [.. newTags];

                // We're already in UI thread, so just raise the events
                OnPropertyChanged(nameof(Tags));
                OnPropertyChanged(nameof(HasTags));
            });
    }

    private void UpdateShowsTitle()
    {
        var oldShowTitle = ShowTitle;
        ShowTitle = LayoutShowsTitle;
        if (oldShowTitle != ShowTitle)
        {
            UpdateProperty(nameof(ShowTitle));
        }
    }

    private void UpdateShowsSubtitle()
    {
        var oldShowSubtitle = ShowSubtitle;
        ShowSubtitle = LayoutShowsSubtitle && !string.IsNullOrWhiteSpace(Subtitle);
        if (oldShowSubtitle != ShowSubtitle)
        {
            UpdateProperty(nameof(ShowSubtitle));
        }
    }

    protected override void UnsafeCleanup()
    {
        base.UnsafeCleanup();

        // Tags don't have event handlers or anything to cleanup
        Tags?.ForEach(t => t.SafeCleanup());
        Details?.SafeCleanup();

        var model = Model.Unsafe;
        if (model is not null)
        {
            // We don't need to revoke the PropChanged event handler here,
            // because we are just overriding CommandItem's FetchProperty and
            // piggy-backing off their PropChanged
        }
    }

    protected void UpdateAccessibleName()
    {
        AccessibleName = Title + ", " + Subtitle;
        UpdateProperty(nameof(AccessibleName));
    }
}
