// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ContentTagsViewModel(ITagsContent model, WeakReference<IPageContext> context)
    : ObservedContentViewModel<ITagsContent>(model, context)
{
    public IReadOnlyList<TagViewModel> Tags { get; private set; } = [];

    protected override void ReadProperties()
    {
        var tags = new List<TagViewModel>();
        foreach (var tag in Model.Tags ?? [])
        {
            var viewModel = new TagViewModel(tag, PageContext);
            viewModel.InitializeProperties();
            tags.Add(viewModel);
        }

        // Tags are immutable snapshots and do not subscribe to extension events.
        Tags = tags;
        UpdateProperty(nameof(Tags));
    }
}
