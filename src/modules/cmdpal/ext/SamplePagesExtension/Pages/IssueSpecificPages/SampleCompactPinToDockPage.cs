// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension.Pages.IssueSpecificPages;

internal sealed partial class SampleCompactPinToDockPage : ListPage
{
    internal static ListItem PinnableItem { get; } = new(new NoOpCommand
    {
        Id = "samples.compact-pin-to-dock.item",
        Name = "Keep open",
        Icon = new IconInfo("\uE718"),
    })
    {
        Title = "Pin this item to the dock",
        Subtitle = "Press Ctrl+K and choose Pin to Dock. The entire dialog should be visible.",
    };

    public SampleCompactPinToDockPage()
    {
        Name = "Compact Pin to Dock dialog";
        Title = Name;
        Icon = new IconInfo("\uE718");
    }

    public override IListItem[] GetItems() => [PinnableItem];
}
