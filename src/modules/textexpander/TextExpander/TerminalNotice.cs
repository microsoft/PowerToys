// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander;

/// <summary>Says, at the caret, that a snippet with lines wasn't put into a terminal (see <see cref="InjectionPolicy.RefusesTypedLines"/>).</summary>
internal static class TerminalNotice
{
    /// <summary>
    /// Gets the notice's heading and message: the same heading either way, and why, for a paste that was refused, or for
    /// copied text with lines under Always type (<paramref name="alwaysType"/>).
    /// </summary>
    public static (string Heading, string Message) Refusal(bool alwaysType)
        => (EngineText.Get("Notice_Heading_TerminalLines", "Couldn't insert this snippet into the terminal"),
            alwaysType
                ? EngineText.Get("Notice_TerminalCopiedLines", "A snippet with copied text and more than one line goes into a terminal only as a paste, and Insertion method is set to Always type, so the trigger was left as you typed it.")
                : EngineText.Get("Notice_TerminalLines", "A snippet with more than one line goes into a terminal only as a paste, and the clipboard couldn't be used just now, so the trigger was left as you typed it."));

    /// <summary>Shows the notice at the caret in <paramref name="target"/>. Not from the hook callback.</summary>
    public static void ShowRefusal(IntPtr target, bool alwaysType)
    {
        (string heading, string message) = Refusal(alwaysType);
        NoticeBubble.Show(target, heading, message);
    }
}
