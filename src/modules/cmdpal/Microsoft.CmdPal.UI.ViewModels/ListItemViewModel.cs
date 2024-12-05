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
    public ITag[] Tags { get; private set; } = [];

    public bool HasTags => Tags.Length > 0;

    public override void InitializeProperties()
    {
        base.InitializeProperties();

        // TODO load tags here, details, suggested text, all that
        var li = _listItemModel.Unsafe;
        if (li == null)
        {
            return; // throw?
        }

        // TODO TagViewModel not ITag
        Tags = li.Tags ?? [];
    }
}
