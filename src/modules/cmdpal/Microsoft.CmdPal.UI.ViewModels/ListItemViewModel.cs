// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ListItemViewModel(IListItem model, IPageContext context)
    : CommandItemViewModel(new(model), context)
{
    public ExtensionObject<IListItem> Model { get; } = new(model);

    // Remember - "observable" properties from the model (via PropChanged)
    // cannot be marked [ObservableProperty]
    public List<TagViewModel> Tags { get; private set; } = [];

    public bool HasTags => Tags.Count > 0;

    public string TextToSuggest { get; private set; } = string.Empty;

    public string Section { get; private set; } = string.Empty;

    public DetailsViewModel? Details { get; private set; }

    [MemberNotNullWhen(true, nameof(Details))]
    public bool HasDetails => Details != null;

    public override void InitializeProperties()
    {
        base.InitializeProperties();

        var li = Model.Unsafe;
        if (li == null)
        {
            return; // throw?
        }

        Tags = li.Tags?.Select(t =>
        {
            var vm = new TagViewModel(t, PageContext);
            vm.InitializeProperties();
            return vm;
        })
            .ToList() ?? [];
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

        UpdateProperty(nameof(HasTags));
        UpdateProperty(nameof(Tags));
        UpdateProperty(nameof(TextToSuggest));
        UpdateProperty(nameof(Section));
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
                Tags = model.Tags?.Select(t =>
                {
                    var vm = new TagViewModel(t, PageContext);
                    vm.InitializeProperties();
                    return vm;
                })
                    .ToList() ?? [];
                UpdateProperty(nameof(HasTags));
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
}
