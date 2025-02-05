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
        return [
            new ListItem(new NoOpCommand()) { Title = "TODO: Implement your extension here" },
            new ListItem(new SampleListPageWithDetails()) { Title = "This one has a subtitle too", Subtitle = "Example Subtitle" },
            new ListItem(new SampleMarkdownPage())
            {
                Title = "This one has a tag too",
                Subtitle = "the one with a tag",
                Tags = [new Tag()
                        {
                            Text = "Sample Tag",
                        }
                ],
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
        ];
    }
}
