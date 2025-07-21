// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Core.ViewModels.Messages;
using Microsoft.CmdPal.Core.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Core.ViewModels;

public partial class ListItemViewModel(IListItem model, WeakReference<IPageContext> context)
    : CommandItemViewModel(new(model), context)
{
    public new ExtensionObject<IListItem> Model { get; } = new(model);

    public List<TagViewModel>? Tags { get; set; }

    // Remember - "observable" properties from the model (via PropChanged)
    // cannot be marked [ObservableProperty]
    public bool HasTags => (Tags?.Count ?? 0) > 0;

    public string TextToSuggest { get; private set; } = string.Empty;

    public string Section { get; private set; } = string.Empty;

    public DetailsViewModel? Details { get; private set; }

    [MemberNotNullWhen(true, nameof(Details))]
    public bool HasDetails => Details != null;

    public override void InitializeProperties()
    {
        if (IsInitialized)
        {
            return;
        }

        // This sets IsInitialized = true
        base.InitializeProperties();

        var li = Model.Unsafe;
        if (li == null)
        {
            return; // throw?
        }

        UpdateTags(li.Tags);

        TextToSuggest = li.TextToSuggest;
        Section = li.Section ?? string.Empty;
        var extensionDetails = li.Details;
        if (extensionDetails != null)
        {
            Details = new(extensionDetails, PageContext);
            Details.InitializeProperties();
            UpdateProperty(nameof(Details));
            UpdateProperty(nameof(HasDetails));
        }

        UpdateProperty(nameof(TextToSuggest));
        UpdateProperty(nameof(Section));
    }

    public override void SlowInitializeProperties()
    {
        // Call the base class first to populate MoreCommands from the model
        base.SlowInitializeProperties();

        // Add "Show Details" action if the page has ShowDetails=false and this item has Details
        if (HasDetails && ShouldAddShowDetailsAction())
        {
            AddShowDetailsAction();
        }
    }

    private bool ShouldAddShowDetailsAction()
    {
        // Check if the parent page has ShowDetails = false
        if (PageContext.TryGetTarget(out var pageContext) && pageContext is ListViewModel listViewModel)
        {
            return !listViewModel.ShowDetails;
        }
        return false;
    }

    private void AddShowDetailsAction()
    {
        if (Details == null) return;

        // Check if "Show Details" action already exists to prevent duplicates
        if (MoreCommands.Any(cmd => cmd is CommandContextItemViewModel ccvm && 
                                    ccvm.Name == "ShowDetails"))
        {
            return;
        }

        // Create a "Show Details" context action
        var showDetailsAction = new CommandContextItem(
            title: "Show Details",
            subtitle: "View detailed information",
            name: "ShowDetails",
            action: () =>
            {
                // Send the ShowDetailsMessage when the action is invoked
                WeakReferenceMessenger.Default.Send<ShowDetailsMessage>(new(Details));
            }
        );

        // Create the view model for the context action
        var showDetailsContextItem = new CommandContextItemViewModel(showDetailsAction, PageContext);
        showDetailsContextItem.InitializeProperties();

        // Create a new list with the "Show Details" action first, followed by existing actions
        var newContextMenu = new List<IContextItemViewModel> { showDetailsContextItem };
        newContextMenu.AddRange(MoreCommands);

        // Update the MoreCommands list in place
        lock (MoreCommands)
        {
            ListHelpers.InPlaceUpdateList(MoreCommands, newContextMenu);
        }

        // Update properties to reflect the changes
        UpdateProperty(nameof(MoreCommands));
        UpdateProperty(nameof(HasMoreCommands));
        UpdateProperty(nameof(AllCommands));
    }

    protected override void FetchProperty(string propertyName)
    {
        base.FetchProperty(propertyName);

        var model = this.Model.Unsafe;
        if (model == null)
        {
            return; // throw?
        }

        switch (propertyName)
        {
            case nameof(Tags):
                UpdateTags(model.Tags);
                break;
            case nameof(TextToSuggest):
                this.TextToSuggest = model.TextToSuggest ?? string.Empty;
                break;
            case nameof(Section):
                this.Section = model.Section ?? string.Empty;
                break;
            case nameof(Details):
                var extensionDetails = model.Details;
                Details = extensionDetails != null ? new(extensionDetails, PageContext) : null;
                Details?.InitializeProperties();
                UpdateProperty(nameof(Details));
                UpdateProperty(nameof(HasDetails));
                break;
        }

        UpdateProperty(propertyName);
    }

    // TODO: Do we want filters to match descriptions and other properties? Tags, etc... Yes?
    // TODO: Do we want to save off the score here so we can sort by it in our ListViewModel?
    public bool MatchesFilter(string filter) => StringMatcher.FuzzySearch(filter, Title).Success || StringMatcher.FuzzySearch(filter, Subtitle).Success;

    public override string ToString() => $"{Name} ListItemViewModel";

    public override bool Equals(object? obj) => obj is ListItemViewModel vm && vm.Model.Equals(this.Model);

    public override int GetHashCode() => Model.GetHashCode();

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
                Tags = new(newTags);

                UpdateProperty(nameof(Tags));
                UpdateProperty(nameof(HasTags));
            });
    }

    protected override void UnsafeCleanup()
    {
        base.UnsafeCleanup();

        // Tags don't have event handlers or anything to cleanup
        Tags?.ForEach(t => t.SafeCleanup());
        Details?.SafeCleanup();

        var model = Model.Unsafe;
        if (model != null)
        {
            // We don't need to revoke the PropChanged event handler here,
            // because we are just overriding CommandItem's FetchProperty and
            // piggy-backing off their PropChanged
        }
    }
}
