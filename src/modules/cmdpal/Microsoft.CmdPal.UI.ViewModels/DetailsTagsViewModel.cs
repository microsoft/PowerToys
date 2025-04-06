// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class DetailsTagsViewModel(
    IDetailsElement _detailsElement,
    WeakReference<IPageContext> context) : DetailsElementViewModel(_detailsElement, context)
{
    public List<TagViewModel> Tags { get; private set; } = [];

    public bool HasTags => Tags.Count > 0;

    private readonly ExtensionObject<IDetailsTags> _dataModel =
        new(_detailsElement.Data as IDetailsTags);

    public override void InitializeProperties()
    {
        base.InitializeProperties();
        var model = _dataModel.Unsafe;
        if (model == null)
        {
            return;
        }

        Tags = model
            .Tags?
            .Select(t =>
        {
            var vm = new TagViewModel(t, PageContext);
            vm.InitializeProperties();
            return vm;
        })
            .ToList() ?? [];
        UpdateProperty(nameof(HasTags));
        UpdateProperty(nameof(Tags));
    }
}
