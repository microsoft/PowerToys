// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// The "wait for space or punctuation" matching rule: a trigger fires only after a terminator, which by
/// default is given back after the replacement. Enter and Tab reset the buffer, so they never terminate.
/// </summary>
internal static class WordBoundary
{
    // Volatile: flipped by the settings watcher, read by the hook on every keystroke.
    private static volatile bool _required = true;
    private static volatile bool _keepTerminator = true;

    /// <summary>Gets or sets a value indicating whether a trigger must be followed by a word terminator before it fires.</summary>
    public static bool Required
    {
        get => _required;
        set => _required = value;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the terminator that released the trigger is given back after
    /// the replacement. Only consulted when <see cref="Required"/> is on.
    /// </summary>
    public static bool KeepTerminator
    {
        get => _keepTerminator;
        set => _keepTerminator = value;
    }

    /// <summary>A character that can sit inside a word (underscore included).</summary>
    public static bool IsWordCharacter(char c)
        => WordBoundaryRules.IsWordCharacter(c);

    /// <summary>
    /// A character that ends a word and so can release a pending trigger: whitespace, punctuation or a symbol,
    /// but not the underscore, which is part of the word.
    /// </summary>
    public static bool IsTerminator(char c)
        => WordBoundaryRules.IsTerminator(c);

    /// <summary>
    /// Whether <paramref name="trigger"/> begins at a word start, given the text before it. A trigger starting
    /// with punctuation carries its own boundary, and an empty <paramref name="textBefore"/> counts as one.
    /// </summary>
    public static bool StartsAtWordBoundary(ReadOnlySpan<char> textBefore, string trigger)
        => WordBoundaryRules.StartsAtWordBoundary(textBefore, trigger);
}
