// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.TimeDate.Pages;

#pragma warning disable SA1402 // The page is an implementation detail of the band item.

/// <summary>
/// A dock band whose contents run live updates only while CmdPal is rendering it.
/// CmdPal attaches an items-changed handler to a band's page when the band goes on
/// screen and removes it when the band goes away; that pairing is the only signal an
/// extension gets, so it is what drives <paramref name="onLoaded"/>/<paramref name="onUnloaded"/>.
/// Bands are constructed for every clock the user has defined, whether or not any of
/// them are pinned, so without this an unpinned clock would tick for the whole session.
/// </summary>
internal sealed partial class OnLoadDockBandItem : CommandItem
{
    private readonly string _bandTitle;
    private readonly OnLoadDockBandPage _page;

    public override string Title => _bandTitle;

    public override ICommand? Command => _page;

    internal OnLoadDockBandItem(IListItem[] items, string id, string bandTitle, Action onLoaded, Action onUnloaded)
    {
        _bandTitle = bandTitle;
        _page = new OnLoadDockBandPage(items, id, bandTitle, onLoaded, onUnloaded);
    }
}

internal sealed partial class OnLoadDockBandPage : OnLoadDynamicListPage
{
    private readonly IListItem[] _items;
    private readonly Action _onLoaded;
    private readonly Action _onUnloaded;

    internal OnLoadDockBandPage(IListItem[] items, string id, string name, Action onLoaded, Action onUnloaded)
    {
        _items = items;
        _onLoaded = onLoaded;
        _onUnloaded = onUnloaded;
        Id = id;
        Name = name;
    }

    public override IListItem[] GetItems() => _items;

    // A band has no search box, so its contents never depend on the query.
    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
    }

    protected override void Loaded() => _onLoaded();

    protected override void Unloaded() => _onUnloaded();
}

#pragma warning restore SA1402 // File may only contain a single type
