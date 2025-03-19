// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension;

internal sealed partial class SampleListPage : ListPage
{
    public SampleListPage()
    {
        Icon = new IconInfo("\uEA37");
        Name = "Sample List Page";
    }

    public override IListItem[] GetItems()
    {
        var confirmOnceArgs = new ConfirmationArgs
        {
            PrimaryCommand = new AnonymousCommand(
                () =>
                {
                    var t = new ToastStatusMessage("The dialog was confirmed");
                    t.Show();
                })
            {
                Name = "Confirm",
                Result = CommandResult.KeepOpen(),
            },
            Title = "You can set a title for the dialog",
            Description = "Are you really sure you want to do the thing?",
        };
        var confirmTwiceArgs = new ConfirmationArgs
        {
            PrimaryCommand = new AnonymousCommand(() => { })
            {
                Name = "How sure are you?",
                Result = CommandResult.Confirm(confirmOnceArgs),
            },
            Title = "You can ask twice too",
            Description = "You probably don't want to though, that'd be annoying.",
        };

        return [
            new ListItem(new NoOpCommand())
            {
                Title = "This is a basic item in the list",
                Subtitle = "I don't do anything though",
            },
            new ListItem(new SampleListPageWithDetails())
            {
                Title = "This item will take you to another page",
                Subtitle = "This allows for nested lists of items",
            },
            new ListItem(new SampleMarkdownPage())
            {
                Title = "Items can have tags",
                Subtitle = "and I'll take you to a page with markdown content",
                Tags = [new Tag("Sample Tag")],
            },
            new ListItem(new SendMessageCommand())
            {
                Title = "I send lots of messages",
                Subtitle = "Status messages can be used to provide feedback to the user in-app",
            },
            new SendSingleMessageItem(),
            new ListItem(new IndeterminateProgressMessageCommand())
            {
                Title = "Do a thing with a spinner",
                Subtitle = "Messages can have progress spinners, to indicate something is happening in the background",
            },
            new ListItem(
                new AnonymousCommand(() => { })
                {
                    Result = CommandResult.Confirm(confirmOnceArgs),
                })
            {
                Title = "Confirm before doing something",
            },
            new ListItem(
                new AnonymousCommand(() => { })
                {
                    Result = CommandResult.Confirm(confirmTwiceArgs),
                })
            {
                Title = "Confirm twice before doing something",
            }
        ];
    }
}
