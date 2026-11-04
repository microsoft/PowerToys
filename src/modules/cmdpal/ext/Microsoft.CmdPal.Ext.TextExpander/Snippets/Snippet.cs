// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.PowerToys.TextExpander;

namespace Microsoft.CmdPal.Ext.TextExpander.Snippets;

/// <summary>What Command Palette can do with a snippet.</summary>
internal enum SnippetKind
{
    /// <summary>Pastes as it is.</summary>
    Ready,

    /// <summary>Asks one or more questions first.</summary>
    AsksQuestions,

    /// <summary>Uses a spot only Text Expander can fill in, so it's listed but not inserted.</summary>
    TextExpanderOnly,
}

/// <summary>One snippet Text Expander would expand: its trigger, its text as saved, and what it needs.</summary>
internal sealed class Snippet
{
    public Snippet(string trigger, string replacement)
    {
        ArgumentException.ThrowIfNullOrEmpty(trigger);
        ArgumentNullException.ThrowIfNull(replacement);

        Trigger = trigger;
        Replacement = replacement;
        Questions = SnippetText.FindQuestions(replacement);
        UsesClipboardParts = SnippetText.UsesClipboardParts(replacement);
        Kind = TextExpanderOnlyVariables.AreUsedIn(replacement)
            ? SnippetKind.TextExpanderOnly
            : Questions.Count > 0 ? SnippetKind.AsksQuestions : SnippetKind.Ready;
    }

    public string Trigger { get; }

    /// <summary>Gets the text as saved, variables and all.</summary>
    public string Replacement { get; }

    public IReadOnlyList<PromptRequest> Questions { get; }

    /// <summary>
    /// Gets a value indicating whether the snippet takes part of the copy, such as an email address, so it opens a page
    /// to pick one when the copy has several.
    /// </summary>
    public bool UsesClipboardParts { get; }

    public SnippetKind Kind { get; }
}
