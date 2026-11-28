// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander;

/// <summary>One match or copy in the choice window: all of its text, its number in the list, and what the list shows of it.</summary>
public sealed class ClipboardChoiceOption
{
    internal ClipboardChoiceOption(string text, int number = 1)
    {
        Text = text ?? string.Empty;
        Number = number.ToString(System.Globalization.CultureInfo.CurrentCulture);
        Shown = ClipboardChoiceText.OneLine(Text);
        Spoken = ClipboardChoiceText.Capped(Text, ClipboardChoiceText.MaxSpoken);
        Tip = ClipboardChoiceText.Capped(Text, ClipboardChoiceText.MaxTip);
    }

    /// <summary>Gets what's inserted when it's picked.</summary>
    public string Text { get; }

    /// <summary>Gets its number in the list, from 1.</summary>
    public string Number { get; }

    /// <summary>Gets the line the list shows.</summary>
    public string Shown { get; }

    /// <summary>Gets its name for screen readers: its text, or as much as is worth hearing of a long one.</summary>
    public string Spoken { get; }

    /// <summary>Gets its tooltip: its text, or the start of a long one.</summary>
    public string Tip { get; }

    public override string ToString() => Spoken;
}
