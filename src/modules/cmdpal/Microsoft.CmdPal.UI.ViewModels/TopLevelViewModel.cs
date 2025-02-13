// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels;

public sealed class TopLevelViewModel
{
    // TopLevelCommandItemWrapper is a ListItem, but it's in-memory for the app already.
    // We construct it either from data that we pulled from the cache, or from the
    // extension, but the data in it is all in our process now.
    private readonly TopLevelCommandItemWrapper _item;

    public IconInfoViewModel Icon { get; private set; }

    public string Title => _item.Title;

    public string Subtitle => _item.Subtitle;

    public TopLevelViewModel(TopLevelCommandItemWrapper item)
    {
        _item = item;
        Icon = new(item.Icon ?? item.Command?.Icon);
        Icon.InitializeProperties();
    }
}
