// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander;

/// <summary>Says, at the caret, that a snippet with lines wasn't put into a terminal (see <see cref="InjectionPolicy.RefusesTypedLines"/>).</summary>
internal static class TerminalNotice
{
    /// <summary>Gets the notice's heading and message.</summary>
    public static (string Heading, string Message) Refusal()
        => (EngineText.Get("Notice_Heading_TerminalLines", "Couldn't insert this snippet into the terminal"),
            EngineText.Get("Notice_TerminalLines", "A snippet with more than one line goes into a terminal only as a paste, and the clipboard couldn't be used just now, so the trigger was left as you typed it."));

    /// <summary>Shows the notice at the caret in <paramref name="target"/>. Not from the hook callback.</summary>
    public static void ShowRefusal(IntPtr target)
    {
        (string heading, string message) = Refusal();
        NoticeBubble.Show(target, heading, message);
    }
}
