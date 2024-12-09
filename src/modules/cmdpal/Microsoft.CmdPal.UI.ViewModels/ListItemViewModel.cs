// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.UI.ViewModels.Models;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ListItemViewModel(IListItem model, TaskScheduler scheduler)
    : CommandItemViewModel(new(model), scheduler)
{
    private readonly ExtensionObject<IListItem> _listItemModel = new(model);

    // Remember - "observable" properties from the model (via PropChanged)
    // cannot be marked [ObservableProperty]
    public List<TagViewModel> Tags { get; private set; } = [];

    public bool HasTags => Tags.Count > 0;

    public string TextToSuggest { get; private set; } = string.Empty;

    public string Section { get; private set; } = string.Empty;

    public override void InitializeProperties()
    {
        base.InitializeProperties();

        var li = _listItemModel.Unsafe;
        if (li == null)
        {
            return; // throw?
        }

        Tags = li.Tags?.Select(t =>
        {
            var vm = new TagViewModel(t, Scheduler);
            vm.InitializeProperties();
            return vm;
        })
            .ToList() ?? [];
        TextToSuggest = li.TextToSuggest;
        Section = li.Section ?? string.Empty;

        UpdateProperty(nameof(HasTags));
        UpdateProperty(nameof(Tags));
        UpdateProperty(nameof(TextToSuggest));
        UpdateProperty(nameof(Section));
    }

    protected override void FetchProperty(string propertyName)
    {
        base.FetchProperty(propertyName);

        var model = this._listItemModel.Unsafe;
        if (model == null)
        {
            return; // throw?
        }

        switch (propertyName)
        {
            case nameof(Tags):
                Tags = model.Tags?.Select(t =>
                {
                    var vm = new TagViewModel(t, Scheduler);
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
        }

        UpdateProperty(propertyName);
    }
}
