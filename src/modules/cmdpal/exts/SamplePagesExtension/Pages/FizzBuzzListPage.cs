// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension;

internal sealed partial class FizzBuzzListPage : ListPage
{
    public override string Title => "FizzBuzz Page";

    public override IconInfo Icon => new("\uE94C"); // % symbol

    public override string Name => "Open";

    private readonly List<ListItem> _items;

    internal FizzBuzzListPage()
    {
        var addNewItem = new ListItem(new AnonymousCommand(() =>
        {
            var c = _items.Count;
            var f = c % 3 == 0;
            var b = c % 5 == 0;
            var s = string.Empty;
            if (f)
            {
                s += "Fizz";
            }

            if (b)
            {
                s += "Buzz";
            }

            _items.Add(new ListItem(new NoOpCommand())
            {
                Title = $"{c}",
                Icon = IconFromIndex(_items.Count),
                Section = s,
            });
            RaiseItemsChanged();
        })
        { Result = CommandResult.KeepOpen() })
        {
            Title = "Add item",
            Subtitle = "Each item will be sorted into sections. Add at least three",
            Icon = new IconInfo("\uED0E"),
        };

        _items = [addNewItem];
    }

    public override IListItem[] GetItems()
    {
        return _items.ToArray();
    }

    private IconInfo IconFromIndex(int index)
    {
        return _icons[index % _icons.Length];
    }

    private readonly IconInfo[] _icons =
        [
            new IconInfo("\ue700"),
            new IconInfo("\ue701"),
            new IconInfo("\ue702"),
            new IconInfo("\ue703"),
            new IconInfo("\ue704"),
            new IconInfo("\ue705"),
            new IconInfo("\ue706"),
            new IconInfo("\ue707"),
            new IconInfo("\ue708"),
            new IconInfo("\ue709"),
            new IconInfo("\ue70a"),
            new IconInfo("\ue70b"),
            new IconInfo("\ue70c"),
            new IconInfo("\ue70d"),
            new IconInfo("\ue70e"),
            new IconInfo("\ue70f"),
            new IconInfo("\ue710"),
            new IconInfo("\ue711"),
            new IconInfo("\ue712"),
            new IconInfo("\ue713"),
        ];
}
