// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension.Pages;

public sealed partial class SampleTimeCapturePage : ListPage
{
    public SampleTimeCapturePage(string state)
    {
        Name = "Browse history";
        var dateTimeOffset = DateTimeOffset.Now;
        Title = "State captured at " + dateTimeOffset;
        Icon = new IconInfo("\ued37");
        EmptyContent = new CommandItem(state, "This was the world at " + dateTimeOffset);
    }

    public override IListItem[] GetItems() => [];
}
