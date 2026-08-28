// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension;

internal sealed partial class SampleLazyDetailsPage : ListPage
{
    private readonly IListItem[] _items =
    [
        new ListItem(new NoOpCommand())
        {
            Title = "How to test lazy details",
            Subtitle = "Select another item to start its simulated load",
            Details = new ContentDetails
            {
                Content =
                [
                    new HeaderContent { Title = "Lazy details samples", Subtitle = "No work starts until an item's details are requested." },
                    new MarkdownContent(
                        "Select a delayed item, expand its preview, then switch to another item before it finishes. " +
                        "Each item keeps its own cached result. Reselecting does not restart the sample's work.\n\n" +
                        "The failure sample fails once and offers Retry. Loaded samples offer Load again. " +
                        "Loading and errors are ordinary Markdown here; this does not add a host loading-state API."),
                ],
            },
        },
        new ListItem(new NoOpCommand())
        {
            Title = "Preview, then load in 2 seconds",
            Subtitle = "The preview appears immediately and stays when the result arrives",
            Details = new SampleDeferredDetails("Two-second load", () => Task.Delay(TimeSpan.FromSeconds(2))),
        },
        new ListItem(new NoOpCommand())
        {
            Title = "Preview, then load in 5 seconds",
            Subtitle = "Expand the preview or switch items while waiting",
            Details = new SampleDeferredDetails("Five-second load", () => Task.Delay(TimeSpan.FromSeconds(5))),
        },
        new ListItem(new NoOpCommand())
        {
            Title = "Fail once, then retry",
            Subtitle = "A simulated failure after 2 seconds; retry succeeds",
            Details = new SampleDeferredDetails("Failure and retry", () => Task.Delay(TimeSpan.FromSeconds(2)), failFirstAttempt: true),
        },
    ];

    public SampleLazyDetailsPage()
    {
        Name = "Open";
        Title = "Lazy details loading";
        Icon = new IconInfo("\uE916");
        ShowDetails = true;
    }

    public override IListItem[] GetItems() => _items;
}
