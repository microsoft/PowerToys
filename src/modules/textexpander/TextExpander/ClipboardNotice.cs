// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// Tells the user, in a notice at the caret, why a clipboard variable's spot was left empty once its snippet went in, such
/// as no email address in what they copied, or clipboard history off. Never what was copied. The words come from the
/// engine's resources (<see cref="EngineText"/>), as whole sentences, so translators never get fragments.
/// </summary>
internal static class ClipboardNotice
{
    /// <summary>
    /// Gets the notice for the spots a snippet left empty: a heading, and a message of whole sentences, one line for each
    /// reason, in the snippet's order. Null when nothing was left empty.
    /// </summary>
    public static (string Heading, string Message)? For(IReadOnlyList<ClipboardGap> gaps)
    {
        ArgumentNullException.ThrowIfNull(gaps);
        if (gaps.Count == 0)
        {
            return null;
        }

        var reasons = new List<(ClipboardGap Gap, int Spots)>();
        foreach (ClipboardGap gap in gaps)
        {
            ClipboardGap reason = ReasonOf(gap);
            int seen = reasons.FindIndex(known => known.Gap == reason);
            if (seen < 0)
            {
                reasons.Add((reason, 1));
            }
            else
            {
                reasons[seen] = (reason, reasons[seen].Spots + 1);
            }
        }

        string message = string.Join("\n", reasons.Select(reason => Message(reason.Gap, several: reason.Spots > 1)));
        return (reasons.Count == 1 ? Heading(reasons[0].Gap) : EngineText.Get("Notice_Heading_Several", "Some spots were left empty"), message);
    }

    /// <summary>
    /// Shows the notice for <paramref name="gaps"/> at the caret in <paramref name="target"/>, if anything was left empty.
    /// On the expansion worker, once the insertion and its guard are done, never on the hook's thread.
    /// </summary>
    public static void Show(IntPtr target, IReadOnlyList<ClipboardGap> gaps)
    {
        if (For(gaps) is (string heading, string message))
        {
            NoticeBubble.Show(target, heading, message);
        }
    }

    // The part matters only when it wasn't found: clipboard history that's off, empty or unreadable is so for any part.
    private static ClipboardGap ReasonOf(ClipboardGap gap) => gap.Reason == ClipboardGapReason.NotFound ? gap : gap with { Part = ClipboardPart.All };

    private static string Heading(ClipboardGap gap) => gap.Reason switch
    {
        ClipboardGapReason.HistoryOff => EngineText.Get("Notice_Heading_HistoryOff", "Clipboard history is off"),
        ClipboardGapReason.NoCopies => EngineText.Get("Notice_Heading_NoCopies", "No copies in clipboard history"),
        ClipboardGapReason.HistoryUnreadable => EngineText.Get("Notice_Heading_HistoryUnreadable", "Couldn't read clipboard history"),
        _ => gap.Part switch
        {
            ClipboardPart.Email => EngineText.Get("Notice_Heading_NoEmail", "No email address found"),
            ClipboardPart.Link => EngineText.Get("Notice_Heading_NoLink", "No link found"),
            ClipboardPart.Phone => EngineText.Get("Notice_Heading_NoPhone", "No phone number found"),
            ClipboardPart.Number => EngineText.Get("Notice_Heading_NoNumber", "No number found"),
            _ => EngineText.Get("Notice_Heading_NoText", "No text copied"),
        },
    };

    private static string Message(ClipboardGap gap, bool several) => (gap.Reason, gap.Part, several) switch
    {
        (ClipboardGapReason.HistoryOff, _, false) => EngineText.Get("Notice_HistoryOff_One", "Clipboard history is off, so that spot was left empty."),
        (ClipboardGapReason.HistoryOff, _, true) => EngineText.Get("Notice_HistoryOff_Several", "Clipboard history is off, so those spots were left empty."),
        (ClipboardGapReason.NoCopies, _, false) => EngineText.Get("Notice_NoCopies_One", "Your clipboard history has no text copies, so that spot was left empty."),
        (ClipboardGapReason.NoCopies, _, true) => EngineText.Get("Notice_NoCopies_Several", "Your clipboard history has no text copies, so those spots were left empty."),
        (ClipboardGapReason.HistoryUnreadable, _, false) => EngineText.Get("Notice_HistoryUnreadable_One", "Text Expander couldn't read your clipboard history in time, so that spot was left empty."),
        (ClipboardGapReason.HistoryUnreadable, _, true) => EngineText.Get("Notice_HistoryUnreadable_Several", "Text Expander couldn't read your clipboard history in time, so those spots were left empty."),
        (_, ClipboardPart.Email, false) => EngineText.Get("Notice_NoEmail_One", "There's no email address in what you copied, so that spot was left empty."),
        (_, ClipboardPart.Email, true) => EngineText.Get("Notice_NoEmail_Several", "There's no email address in what you copied, so those spots were left empty."),
        (_, ClipboardPart.Link, false) => EngineText.Get("Notice_NoLink_One", "There's no link in what you copied, so that spot was left empty."),
        (_, ClipboardPart.Link, true) => EngineText.Get("Notice_NoLink_Several", "There's no link in what you copied, so those spots were left empty."),
        (_, ClipboardPart.Phone, false) => EngineText.Get("Notice_NoPhone_One", "There's no phone number in what you copied, so that spot was left empty."),
        (_, ClipboardPart.Phone, true) => EngineText.Get("Notice_NoPhone_Several", "There's no phone number in what you copied, so those spots were left empty."),
        (_, ClipboardPart.Number, false) => EngineText.Get("Notice_NoNumber_One", "There's no number in what you copied, so that spot was left empty."),
        (_, ClipboardPart.Number, true) => EngineText.Get("Notice_NoNumber_Several", "There's no number in what you copied, so those spots were left empty."),
        (_, _, false) => EngineText.Get("Notice_NoText_One", "There's no text on your clipboard, so that spot was left empty."),
        (_, _, true) => EngineText.Get("Notice_NoText_Several", "There's no text on your clipboard, so those spots were left empty."),
    };
}
