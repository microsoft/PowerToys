// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CompatibilityExtension;

internal sealed partial class HomePage : ListPage
{
    private readonly IListItem[] _items;

    public HomePage()
    {
        Id = $"compat.{Baseline.SdkVersion}.home";
        Name = "Open";
        Title = $"Compatibility SDK {Baseline.SdkVersion}";
        Icon = new IconInfo("\uE9D9");
        _items =
        [
            new ListItem(new ActionsPage()) { Title = "01 - List and actions", Subtitle = "Selection, icons, tags, context menu, shortcuts and confirmation" },
            new ListItem(new SearchPage()) { Title = "02 - Search and filters", Subtitle = "Dynamic results, filter changes and no matches" },
            new ListItem(new DetailsPage()) { Title = "03 - Details", Subtitle = "Markdown, metadata and clearing stale details" },
            new ListItem(new MarkdownPage()) { Title = "04 - Markdown", Subtitle = "Headings, lists, table, code and content updates" },
            new ListItem(new FormPage()) { Title = "05 - Form", Subtitle = "Text, choice, toggle, submission and mixed content" },
            new ListItem(new EmptyPage()) { Title = "06 - Empty list", Subtitle = "Empty-content title, subtitle and icon" },
        ];
    }

    public override IListItem[] GetItems() => _items;
}
