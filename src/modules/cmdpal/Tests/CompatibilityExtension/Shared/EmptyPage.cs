// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CompatibilityExtension;

internal sealed partial class EmptyPage : ListPage
{
    public EmptyPage()
    {
        Id = "compat.empty";
        Name = "Open";
        Title = "Empty list";
        EmptyContent = new CommandItem(new NoOpCommand())
        {
            Title = "This list is intentionally empty",
            Subtitle = "The empty state should remain visible while typing",
            Icon = new IconInfo("\uE7C3"),
        };
    }

    public override IListItem[] GetItems() => [];
}
