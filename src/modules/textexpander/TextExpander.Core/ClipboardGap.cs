// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

namespace Microsoft.PowerToys.TextExpander;

/// <summary>Why a clipboard variable inserted nothing.</summary>
public enum ClipboardGapReason
{
    /// <summary>
    /// What it inserts isn't there: no text on the clipboard for {{clipboard}}, or no email address, link, phone number or
    /// number in what was copied, the copy on the clipboard now or the recent copies in clipboard history.
    /// </summary>
    NotFound,

    /// <summary>It picks from clipboard history, which is off.</summary>
    HistoryOff,

    /// <summary>It picks from clipboard history, which has no text copies.</summary>
    NoCopies,

    /// <summary>It picks from clipboard history, which couldn't be read in time.</summary>
    HistoryUnreadable,
}

/// <summary>
/// A spot a clipboard variable left empty when its snippet expanded, and why, so the user can be told. The rest of the
/// snippet still goes in.
/// </summary>
/// <param name="Part">What the variable inserts.</param>
/// <param name="Reason">Why it inserted nothing.</param>
public readonly record struct ClipboardGap(ClipboardPart Part, ClipboardGapReason Reason);
