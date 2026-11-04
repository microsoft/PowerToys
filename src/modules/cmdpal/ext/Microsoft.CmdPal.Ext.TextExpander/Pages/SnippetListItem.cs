// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.TextExpander.Snippets;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.TextExpander.Pages;

/// <summary>A snippet on the snippets page: its trigger, a one-line preview, and its whole text in the details.</summary>
internal sealed partial class SnippetListItem : ListItem
{
    public SnippetListItem(Snippet snippet, SnippetActions actions)
        : base(actions.PrimaryCommand(snippet))
    {
        ArgumentNullException.ThrowIfNull(snippet);

        Snippet = snippet;
        var preview = SnippetText.Preview(snippet.Replacement);
        Title = snippet.Trigger;
        Subtitle = SnippetText.OneLine(preview);
        Icon = Icons.TextExpander;
        Tags = actions.Tags(snippet);
        MoreCommands = actions.MoreCommands(snippet);
        Details = new Details
        {
            Title = snippet.Trigger,
            Body = DetailsBody(preview),
        };
    }

    public Snippet Snippet { get; }

    /// <summary>Shows <paramref name="text"/> exactly as it is, line breaks and spaces included, in a code block.</summary>
    internal static string DetailsBody(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text.Length == 0)
        {
            return string.Empty;
        }

        // The fence must be longer than any run of backticks in the text.
        var fence = "```";
        while (text.Contains(fence, StringComparison.Ordinal))
        {
            fence += "`";
        }

        return $"{fence}text\n{text}\n{fence}";
    }
}
