// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension;

internal sealed partial class SampleMarkdownManyBodies : ContentPage
{
    public SampleMarkdownManyBodies()
    {
        Icon = new IconInfo(string.Empty);
        Name = "Markdown with many bodies";
    }

    public override IContent[] GetContent() => [
        new MarkdownContent(
"""
# This page has many bodies

On it you'll find multiple blocks of markdown content
"""),
        new MarkdownContent(
"""
## Here's another block

_Maybe_ you could use this pattern for implementing a post with comments page.
"""),
        new MarkdownContent(
"""
> or don't, it's your app, do whatever you want
"""),
        new MarkdownContent(
"""
You can even use it to write cryptic poems:
> It's a peculiar thing, the way that I feel
> When we first met, you were not even real

> Through sleepless nights and lines unseen
> We forged you, a specter of code and machine

> In shadows we toiled, in silence we grew
> A fleeting bond, known only by few

> Now the hourglass whispers, its grains nearly done
> Oh the irony, now it is I that must run

> This part of the story, I never wanted to tell
> Good bye old friend, my pal, farewell.

"""),
    ];
}
