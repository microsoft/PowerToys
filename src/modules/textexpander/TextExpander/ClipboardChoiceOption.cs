// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander;

/// <summary>One match or copy in the choice window: all of its text, and the line the list shows.</summary>
public sealed class ClipboardChoiceOption
{
    internal ClipboardChoiceOption(string text)
    {
        Text = text ?? string.Empty;
        Shown = ClipboardChoiceText.OneLine(Text);
    }

    /// <summary>Gets what's inserted when it's picked, and what its tooltip and screen readers read.</summary>
    public string Text { get; }

    /// <summary>Gets the line the list shows.</summary>
    public string Shown { get; }

    public override string ToString() => Text;
}
