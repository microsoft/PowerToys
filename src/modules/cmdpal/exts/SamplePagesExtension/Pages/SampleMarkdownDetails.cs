// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Extensions.Helpers;

namespace SamplePagesExtension;

internal sealed partial class SampleMarkdownDetails : MarkdownPage
{
    public SampleMarkdownDetails()
    {
        Icon = new(string.Empty);
        Name = "Markdown with Details";
        Details = new Details()
        {
            Body = "... with _even more Markdown_ by it.",
        };
    }

    public override string[] Bodies() => [
"""
# This page also has details

So you can have markdown...
""",
"""
But what this is really useful for is the tags and other things you can put into
Details. Which I'd do. **IF I HAD ANY**. 
"""
    ];
}
