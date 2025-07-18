// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;

namespace SamplePagesExtension;

internal sealed partial class OnLoadPage : IListPage
{
    private readonly List<ListItem> _items = new();

    public IIconInfo Icon => new IconInfo("\uE8AB"); // switch

    public string Title => "Load/Unload sample";

    public string PlaceholderText => "This page changes each time you load it";

    public ICommandItem EmptyContent => new CommandItem() { Icon = new IconInfo("\uE8AB"), Title = "This page starts empty", Subtitle = "but go back and open it again" };

    public IFilters Filters => null;

    public IGridProperties GridProperties => null;

    public bool HasMoreItems => false;

    public string SearchText => string.Empty;

    public bool ShowDetails => false;

    public OptionalColor AccentColor => default;

    public bool IsLoading => false;

    public string Id => string.Empty;

    public string Name => "Open";

#pragma warning disable CS0067 // The event is never used
    public event TypedEventHandler<object, IPropChangedEventArgs> PropChanged;

    private event TypedEventHandler<object, IItemsChangedEventArgs> InternalItemsChanged;
#pragma warning restore CS0067 // The event is never used

    public event TypedEventHandler<object, IItemsChangedEventArgs> ItemsChanged
    {
        add
        {
            InternalItemsChanged += value;
            var nowString = DateTime.Now.ToString("T", CultureInfo.CurrentCulture);
            var item = new ListItem(new NoOpCommand())
            {
                Title = $"Loaded {nowString}",
                Icon = new IconInfo("\uECCB"), // Radio button on
            };
            _items.Add(item);
        }

        remove
        {
            InternalItemsChanged -= value;
            var nowString = DateTime.Now.ToString("T", CultureInfo.CurrentCulture);
            var item = new ListItem(new NoOpCommand())
            {
                Title = $"Unloaded {nowString}",
                Icon = new IconInfo("\uECCA"), // Radio button off
            };
            _items.Add(item);
        }
    }

    public IListItem[] GetItems() => _items.ToArray();

    public void LoadMore()
    {
    }
}
