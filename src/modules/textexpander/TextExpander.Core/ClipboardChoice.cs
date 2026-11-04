// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

namespace Microsoft.PowerToys.TextExpander;

/// <summary>What a snippet asks the user to choose when it runs.</summary>
public enum ClipboardChoiceKind
{
    /// <summary>A copy from clipboard history, for {{clipboard:pick}}.</summary>
    Copy,

    /// <summary>One of two or more matches of a part, for {{clipboard:link:pick}}.</summary>
    Match,
}

/// <summary>
/// A choice a snippet asks for when it runs. The answer is the chosen text, an empty text to insert nothing, or null to
/// cancel the whole snippet, as cancelling a question does.
/// </summary>
/// <param name="Kind">Whether a copy or a match is chosen.</param>
/// <param name="Part">What's inserted from the copy or match: a whole copy, or an email address, link, phone number or number.</param>
/// <param name="Options">For <see cref="ClipboardChoiceKind.Match"/>, the different matches, in order; empty for a copy, which the window reads itself.</param>
/// <param name="IsLast">Whether nothing else is asked after this, so keys typed once it's answered are meant for after the snippet.</param>
public sealed record ClipboardChoice(ClipboardChoiceKind Kind, ClipboardPart Part, IReadOnlyList<string> Options, bool IsLast);
