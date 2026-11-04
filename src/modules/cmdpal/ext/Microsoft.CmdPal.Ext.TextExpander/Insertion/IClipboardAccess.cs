// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.CmdPal.Ext.TextExpander.Insertion;

/// <summary>How a write to the clipboard went.</summary>
internal enum ClipboardWrite
{
    Written,

    /// <summary>The clipboard was emptied but the text didn't go on, so the user's copy has to be put back.</summary>
    Failed,

    /// <summary>Nothing was touched: the clipboard couldn't be opened for writing.</summary>
    Refused,
}

/// <summary>How handing the user's clipboard back after a paste went.</summary>
internal enum ClipboardRestore
{
    /// <summary>Everything went back.</summary>
    Restored,

    /// <summary>Someone else wrote to the clipboard after the paste, so their copy stays and nothing was touched.</summary>
    Superseded,

    /// <summary>Nothing was touched: the clipboard couldn't be opened, or not in a way that may be written to.</summary>
    Untouched,

    /// <summary>The clipboard was emptied, but not everything went back: it changed, by this extension's hand.</summary>
    Incomplete,
}

/// <summary>The clipboard, as a paste borrows it and gives it back.</summary>
internal interface IClipboardAccess
{
    /// <summary>Gets the clipboard's change counter: a change across a paste means someone else copied.</summary>
    uint SequenceNumber { get; }

    /// <summary>Reads the clipboard's text, or null when it holds none.</summary>
    string? ReadText();

    /// <summary>
    /// Copies everything on the clipboard. Returns null when it can't be put back exactly (a format that isn't plain
    /// memory, one that won't render, or more than Text Expander's limit), so the clipboard isn't borrowed at all.
    /// </summary>
    SavedClipboard? Save();

    /// <summary>
    /// Puts <paramref name="text"/> on the clipboard. With <paramref name="keepOutOfHistory"/>, it carries the formats
    /// that keep it out of Clipboard History, Cloud Clipboard and clipboard managers, as Text Expander's pastes do.
    /// </summary>
    ClipboardWrite WriteText(string text, bool keepOutOfHistory);

    /// <summary>True when the clipboard holds exactly <paramref name="text"/>.</summary>
    bool HoldsText(string text);

    /// <summary>
    /// Puts <paramref name="saved"/> back, marked to stay out of Clipboard History unless it already carried any of
    /// those formats. <paramref name="stillOurs"/>, given the sequence number, is asked once the clipboard is open,
    /// before it's emptied: the open may have waited for another app that was copying, and while the clipboard is held
    /// open nobody else can write to it.
    /// </summary>
    ClipboardRestore Restore(SavedClipboard saved, Func<uint, bool> stillOurs);

    /// <summary>Gets the process that has the clipboard open now, or 0 when no one, or only this extension, has.</summary>
    uint OpenedByProcessId();
}
