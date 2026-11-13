// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Microsoft.CmdPal.Ext.TextExpander.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.PowerToys.TextExpander;

namespace Microsoft.CmdPal.Ext.TextExpander.Snippets;

/// <summary>
/// Words for clipboard variables, as Text Expander uses them: a variable's name in a preview, as the snippet editor
/// calls it ("Clipboard", "Link from clipboard"), what a pick page asks, as Text Expander's chooser does ("Pick the link
/// to insert"), and what's said about a spot left empty.
/// </summary>
internal static class ClipboardVariableText
{
    private static readonly CompositeFormat FromNow = CompositeFormat.Parse(Resources.preview_clipboard_from_now);
    private static readonly CompositeFormat FromHistory = CompositeFormat.Parse(Resources.preview_clipboard_from_history);

    public static string Label(ClipboardVariable variable)
    {
        variable = variable.Normalized();
        if (variable.Part == ClipboardPart.All)
        {
            return variable.PickCopy ? Resources.preview_clipboard_whole_history : Resources.preview_clipboard;
        }

        return string.Format(CultureInfo.CurrentCulture, variable.PickCopy ? FromHistory : FromNow, PartName(variable.Part));
    }

    /// <summary>Gets what a pick page asks for <paramref name="part"/>, such as "Pick the email address to insert".</summary>
    public static string PickTitle(ClipboardPart part) => part switch
    {
        ClipboardPart.Email => Resources.picks_title_email,
        ClipboardPart.Link => Resources.picks_title_link,
        ClipboardPart.Phone => Resources.picks_title_phone,
        _ => Resources.picks_title_number,
    };

    public static IconInfo PartIcon(ClipboardPart part) => part switch
    {
        ClipboardPart.Email => Icons.Email,
        ClipboardPart.Link => Icons.Link,
        ClipboardPart.Phone => Icons.Phone,
        _ => Icons.Number,
    };

    /// <summary>
    /// Gets what's said after a paste about the spots Core left empty (<see cref="ExpandedSnippet.Gaps"/>), in the words of
    /// Text Expander's notice: one sentence for each kind of part that wasn't there, in the snippet's order. Null when none
    /// was. Spots that pick from clipboard history never get here, since only Text Expander inserts those snippets.
    /// </summary>
    public static string? NothingFound(IReadOnlyList<ClipboardGap> gaps)
    {
        var parts = new List<(ClipboardPart Part, int Spots)>();
        foreach (var gap in gaps)
        {
            if (gap.Reason != ClipboardGapReason.NotFound)
            {
                continue;
            }

            var seen = parts.FindIndex(known => known.Part == gap.Part);
            if (seen < 0)
            {
                parts.Add((gap.Part, 1));
            }
            else
            {
                parts[seen] = (gap.Part, parts[seen].Spots + 1);
            }
        }

        // Text Expander's notice puts each sentence on its own line; a pick page shows this as one line, so a space.
        return parts.Count == 0 ? null : string.Join(' ', parts.ConvertAll(part => NothingFound(part.Part, several: part.Spots > 1)));
    }

    private static string NothingFound(ClipboardPart part, bool several) => (part, several) switch
    {
        (ClipboardPart.Email, false) => Resources.toast_missing_email,
        (ClipboardPart.Email, true) => Resources.toast_missing_email_several,
        (ClipboardPart.Link, false) => Resources.toast_missing_link,
        (ClipboardPart.Link, true) => Resources.toast_missing_link_several,
        (ClipboardPart.Phone, false) => Resources.toast_missing_phone,
        (ClipboardPart.Phone, true) => Resources.toast_missing_phone_several,
        (ClipboardPart.Number, false) => Resources.toast_missing_number,
        (ClipboardPart.Number, true) => Resources.toast_missing_number_several,
        (_, false) => Resources.toast_missing_clipboard,
        (_, true) => Resources.toast_missing_clipboard_several,
    };

    private static string PartName(ClipboardPart part) => part switch
    {
        ClipboardPart.Email => Resources.preview_clipboard_email,
        ClipboardPart.Link => Resources.preview_clipboard_link,
        ClipboardPart.Phone => Resources.preview_clipboard_phone,
        _ => Resources.preview_clipboard_number,
    };
}
