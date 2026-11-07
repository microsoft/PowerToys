// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// A choice a clipboard variable asks for when its snippet runs, because there are two or more to choose from: matches of a
/// part in what's on the clipboard now, matches of a part across the recent copies in clipboard history, or whole copies
/// from history. The answer is the chosen option, or null to cancel the whole snippet, as cancelling a question does.
/// </summary>
/// <param name="Part">What's chosen: a whole copy, or an email address, link, phone number or number.</param>
/// <param name="FromHistory">Whether the options come from clipboard history rather than what's on the clipboard now.</param>
/// <param name="Options">
/// The options, two or more, each different one once: in order for what's on the clipboard now, newest first from history.
/// At most <see cref="MaxOptions"/>.
/// </param>
/// <param name="IsLast">Whether nothing else is asked after this, so keys typed once it's answered are meant for after the snippet.</param>
public sealed record ClipboardChoice(ClipboardPart Part, bool FromHistory, IReadOnlyList<string> Options, bool IsLast)
{
    /// <summary>The most options a choice offers, so a copy full of numbers doesn't fill a window with thousands.</summary>
    public const int MaxOptions = 100;
}
