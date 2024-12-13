// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Extensions.Helpers;

namespace SamplePagesExtension;

internal sealed partial class SampleMarkdownManyBodies : MarkdownPage
{
    public SampleMarkdownManyBodies()
    {
        Icon = new(string.Empty);
        Name = "Markdown with many bodies";
    }

    public override string[] Bodies() => [
"""
# This page has many bodies

On it you'll find multiple blocks of markdown content
""",
"""
## Here's another block

_Maybe_ you could use this pattern for implementing a post with comments page.  
""",
"""
> or don't, it's your app, do whatever you want
"""
    ];
}
