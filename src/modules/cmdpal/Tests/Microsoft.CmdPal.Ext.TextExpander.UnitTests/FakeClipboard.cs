// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CmdPal.Ext.TextExpander.Insertion;
using Microsoft.PowerToys.TextExpander;

namespace Microsoft.CmdPal.Ext.TextExpander.UnitTests;

/// <summary>A clipboard in memory that records what was done to it.</summary>
internal sealed class FakeClipboard : IClipboardAccess
{
    public string? Text { get; set; }

    public bool CanBeSaved { get; set; } = true;

    public ClipboardWrite NextWrite { get; set; } = ClipboardWrite.Written;

    /// <summary>Gets or sets how many restores empty the clipboard and then fail, before one works.</summary>
    public int FailingRestores { get; set; }

    /// <summary>Gets or sets how many restores can't open the clipboard at all, because another app holds it.</summary>
    public int UnopenableRestores { get; set; }

    /// <summary>Gets or sets what happens while the next restore waits to open the clipboard, such as another app copying.</summary>
    public Action? WhileOpening { get; set; }

    /// <summary>Gets or sets what happens right after a write, such as another window coming to the front.</summary>
    public Action? AfterWrite { get; set; }

    public uint SequenceNumber { get; private set; } = 1;

    /// <summary>Gets a value indicating whether the extension's write is the last one, as the clipboard's owner says.</summary>
    public bool IsOurs { get; private set; }

    public uint OpenedBy { get; set; }

    public int Saves { get; private set; }

    public int Restores { get; private set; }

    /// <summary>Gets how many times the clipboard's text was read.</summary>
    public int Reads { get; private set; }

    public List<(string Text, bool KeepOutOfHistory)> Writes { get; } = new();

    public string? ReadText()
    {
        Reads++;
        return Text;
    }

    public SavedClipboard? Save()
    {
        Saves++;
        if (!CanBeSaved)
        {
            return null;
        }

        return new SavedClipboard(Text is null ? [] : [new ClipboardEntry(ClipboardFormats.CF_UNICODETEXT, Encoding.Unicode.GetBytes(Text + '\0'))]);
    }

    public ClipboardWrite WriteText(string text, bool keepOutOfHistory)
    {
        Writes.Add((text, keepOutOfHistory));
        if (NextWrite == ClipboardWrite.Refused)
        {
            return ClipboardWrite.Refused;
        }

        SequenceNumber++;
        IsOurs = true;
        Text = NextWrite == ClipboardWrite.Written ? text : null;
        AfterWrite?.Invoke();
        return NextWrite;
    }

    public bool HoldsText(string text) => string.Equals(Text, text, StringComparison.Ordinal);

    public ClipboardRestore Restore(SavedClipboard saved, Func<uint, bool> stillOurs)
    {
        Restores++;
        var whileOpening = WhileOpening;
        WhileOpening = null;
        whileOpening?.Invoke();
        if (UnopenableRestores > 0)
        {
            UnopenableRestores--;
            return ClipboardRestore.Untouched;
        }

        if (!stillOurs(SequenceNumber))
        {
            return ClipboardRestore.Superseded;
        }

        SequenceNumber++;
        IsOurs = true;
        if (FailingRestores > 0)
        {
            FailingRestores--;
            Text = null;
            return ClipboardRestore.Incomplete;
        }

        Text = saved.IsEmpty ? null : Encoding.Unicode.GetString(saved.Entries[0].Data).TrimEnd('\0');
        return ClipboardRestore.Restored;
    }

    public uint OpenedByProcessId() => OpenedBy;

    /// <summary>The user copies something else.</summary>
    public void UserCopies(string text)
    {
        SequenceNumber++;
        IsOurs = false;
        Text = text;
    }
}
