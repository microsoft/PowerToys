// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// The "wait for the whole word" matching rule.
///
/// <para>
/// By default a trigger fires the instant its last character is typed. That is the right default
/// — it is what makes expansion feel immediate — but it means a trigger can fire in the middle of
/// a longer word. A trigger of <c>btw</c> goes off while typing <i>a<b>btw</b>ard</i>, and the
/// user's word is destroyed by something they did not ask for.
/// </para>
///
/// <para>
/// With this on, a trigger only fires once it is followed by something that ends a word — a
/// space, or punctuation. The terminator the user typed is not thrown away: it is appended to the
/// replacement, so typing <c>#sig </c> leaves the signature followed by the space, exactly as if
/// the expansion had never happened.
/// </para>
///
/// <para>
/// Enter and Tab cannot act as terminators, because the hook treats them as buffer resets before
/// matching is reached — they move the caret, and a caret move means the buffer no longer
/// describes what is on screen.
/// </para>
/// </summary>
internal static class WordBoundary
{
    // Volatile because the settings watcher thread flips these while the hook thread reads them
    // on every keystroke.
    private static volatile bool _required;
    private static volatile bool _keepTerminator = true;

    /// <summary>Gets or sets a value indicating whether whether a trigger must be followed by a word terminator before it fires.</summary>
    public static bool Required
    {
        get => _required;
        set => _required = value;
    }

    /// <summary>
    /// Gets or sets a value indicating whether whether the terminator that released the trigger is given back after the replacement.
    ///
    /// <para>
    /// On by default, because the user typed that space and not handing it back means typing it
    /// twice. Off matters for the cases raised on the PowerToys issue thread — email addresses
    /// and codes, where a trailing space is not cosmetic but wrong.
    /// </para>
    ///
    /// <para>
    /// Only consulted when <see cref="Required"/> is on. Without word mode there is no terminator
    /// to keep or drop: the trigger's own last keystroke is what fires it.
    /// </para>
    /// </summary>
    public static bool KeepTerminator
    {
        get => _keepTerminator;
        set => _keepTerminator = value;
    }

    /// <summary>
    /// A character that can sit inside a word. Underscore counts, because identifiers are the
    /// one kind of "word" people routinely type where an underscore is not a break.
    /// </summary>
    public static bool IsWordCharacter(char c)
        => char.IsLetterOrDigit(c) || c == '_';

    /// <summary>
    /// A character that ends a word, and so can release a pending trigger.
    ///
    /// <para>
    /// Defined positively rather than as "not a word character" so that control characters and
    /// other oddities cannot silently become terminators. Whitespace covers the space the feature
    /// is named for; punctuation and symbols cover the rest of the natural cases —
    /// <c>#sig.</c> and <c>#sig,</c> should expand just as readily as <c>#sig </c>.
    /// </para>
    /// </summary>
    public static bool IsTerminator(char c)
        => char.IsWhiteSpace(c) || char.IsPunctuation(c) || char.IsSymbol(c);

    /// <summary>
    /// Whether <paramref name="trigger"/> begins at a legitimate word start, given the text
    /// immediately before it.
    ///
    /// <para>
    /// A trigger that itself starts with punctuation — <c>#sig</c>, <c>:shrug</c> — carries its
    /// own boundary, so nothing before it can put it mid-word. Requiring a break before those
    /// too would stop <c>x#sig</c> expanding for no benefit the user would recognise.
    /// </para>
    ///
    /// <para>
    /// An empty <paramref name="textBefore"/> counts as a boundary. It can also mean the rolling
    /// buffer has wrapped, which is only reachable for a word longer than the buffer, and
    /// treating that as a word start is the reading that still expands rather than mysteriously
    /// refusing to.
    /// </para>
    /// </summary>
    public static bool StartsAtWordBoundary(ReadOnlySpan<char> textBefore, string trigger)
    {
        if (trigger.Length == 0 || !IsWordCharacter(trigger[0]))
        {
            return true;
        }

        return textBefore.Length == 0 || !IsWordCharacter(textBefore[^1]);
    }
}
