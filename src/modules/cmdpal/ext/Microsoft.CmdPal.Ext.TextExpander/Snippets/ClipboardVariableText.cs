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
    /// Gets what's said after a paste about the spots Core left empty (<see cref="ExpandedSnippet.Gaps"/>): what wasn't
    /// there, when it's one spot, or that some were left empty. Null when none was. Spots that pick from clipboard history
    /// never get here, since only Text Expander inserts those snippets.
    /// </summary>
    public static string? NothingFound(IReadOnlyList<ClipboardGap> gaps) => gaps.Count switch
    {
        0 => null,
        1 when gaps[0].Reason == ClipboardGapReason.NotFound => gaps[0].Part switch
        {
            ClipboardPart.All => Resources.toast_missing_clipboard,
            ClipboardPart.Email => Resources.toast_missing_email,
            ClipboardPart.Link => Resources.toast_missing_link,
            ClipboardPart.Phone => Resources.toast_missing_phone,
            _ => Resources.toast_missing_number,
        },
        _ => Resources.toast_missing_several,
    };

    private static string PartName(ClipboardPart part) => part switch
    {
        ClipboardPart.Email => Resources.preview_clipboard_email,
        ClipboardPart.Link => Resources.preview_clipboard_link,
        ClipboardPart.Phone => Resources.preview_clipboard_phone,
        _ => Resources.preview_clipboard_number,
    };
}
