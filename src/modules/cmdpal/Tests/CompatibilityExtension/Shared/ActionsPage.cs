// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.System;

namespace CompatibilityExtension;

internal sealed partial class ActionsPage : ListPage
{
    private readonly IListItem[] _items;
    private readonly ListItem _counter;
    private int _count;

    public ActionsPage()
    {
        Id = "compat.actions";
        Name = "Open";
        Title = "List and actions";
        Icon = new IconInfo("\uE8FD");
        _counter = new ListItem(new AnonymousCommand(() => UpdateCount(_count + 1))
        {
            Name = "Increment",
            Result = CommandResult.KeepOpen(),
        })
        {
            Title = "Counter: 0",
            Subtitle = "Enter increments this row without closing the page",
            Icon = new IconInfo("\uE710"),
            Tags = [new Tag("Live")],
            MoreCommands =
            [
                new CommandContextItem(new AnonymousCommand(() => UpdateCount(0))
                {
                    Name = "Reset counter",
                    Result = CommandResult.KeepOpen(),
                })
                {
                    Title = "Reset counter",
                    RequestedShortcut = KeyChordHelpers.FromModifiers(true, false, false, false, (int)VirtualKey.R, 0),
                },
                new CommandContextItem(new AnonymousCommand(() => { })
                {
                    Name = "Show toast",
                    Result = CommandResult.ShowToast("Compatibility toast"),
                }) { Title = "Show toast" },
            ],
        };
        _items =
        [
            _counter,
            new ListItem(new AnonymousCommand(() => { })
            {
                Name = "Confirm",
                Result = CommandResult.Confirm(new ConfirmationArgs
                {
                    Title = "Reset the counter?",
                    Description = "Cancel preserves the counter. Confirm resets it to zero.",
                    PrimaryCommand = new AnonymousCommand(() => UpdateCount(0))
                    {
                        Name = "Reset",
                        Result = CommandResult.KeepOpen(),
                    },
                }),
            }) { Title = "Confirmation dialog", Subtitle = "Exercise both Cancel and Reset", Icon = new IconInfo("\uE946") },
            new ListItem(new MarkdownPage()) { Title = "Nested navigation", Subtitle = "Open Markdown, then go back", Tags = [new Tag("Navigation")] },
            new ListItem(new NoOpCommand())
            {
                Title = "A deliberately long list item title for checking truncation and selection rendering in a narrow Command Palette window",
                Subtitle = "A deliberately long subtitle that should remain readable without overlapping the tags or the command affordance",
                Tags = [new Tag("One"), new Tag("Two")],
            },
        ];
    }

    public override IListItem[] GetItems() => _items;

    private void UpdateCount(int count)
    {
        _count = count;
        _counter.Title = $"Counter: {_count}";
    }
}
