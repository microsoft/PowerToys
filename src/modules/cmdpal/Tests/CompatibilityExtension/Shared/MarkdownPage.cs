// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CompatibilityExtension;

internal sealed partial class MarkdownPage : ContentPage
{
    private readonly MarkdownContent _content = new();
    private int _revision;

    public MarkdownPage()
    {
        Id = "compat.markdown";
        Name = "Open";
        Title = "Markdown";
        Icon = new IconInfo("\uE8A5");
        Commands =
        [
            new CommandContextItem(new AnonymousCommand(() =>
            {
                _revision++;
                UpdateBody();
            })
            {
                Name = "Update content",
                Result = CommandResult.KeepOpen(),
            }) { Title = "Update content" },
        ];
        UpdateBody();
    }

    public override IContent[] GetContent() => [_content];

    private void UpdateBody()
    {
        _content.Body = $$"""
            # Markdown fixture

            SDK **{{Baseline.SdkVersion}}**. Revision **{{_revision}}**.

            ## Formatting

            **Bold**, *italic*, and `inline code`.

            - First bullet
            - Second bullet

            1. First step
            2. Second step

            | Column | Value |
            | --- | --- |
            | Alpha | One |
            | Beta | Two |

            ```text
            A code block
              preserves indentation.
            ```

            > A block quote with text that can wrap when the window is narrow.
            """;
    }
}
