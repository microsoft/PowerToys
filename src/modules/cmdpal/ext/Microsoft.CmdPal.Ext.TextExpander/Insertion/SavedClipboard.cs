// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Microsoft.PowerToys.TextExpander;

namespace Microsoft.CmdPal.Ext.TextExpander.Insertion;

/// <summary>
/// A copy of everything that was on the clipboard, taken before a paste borrows it. Empty means the clipboard was
/// empty, so putting it back empties the clipboard.
/// </summary>
internal sealed partial class SavedClipboard : IDisposable
{
    public SavedClipboard(IReadOnlyList<ClipboardEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        Entries = entries;
    }

    public IReadOnlyList<ClipboardEntry> Entries { get; }

    public bool IsEmpty => Entries.Count == 0;

    /// <summary>Wipes the copied bytes: they may hold something the user copied from a password manager.</summary>
    public void Dispose()
    {
        foreach (var entry in Entries)
        {
            CryptographicOperations.ZeroMemory(entry.Data);
        }
    }
}
