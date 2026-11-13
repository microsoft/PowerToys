// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// An off-clipboard copy of the user's clipboard, taken before an expansion pastes over it. An empty snapshot
/// means the clipboard was genuinely empty, so restoring it clears the clipboard.
/// </summary>
internal sealed class ClipboardSnapshot(IReadOnlyList<ClipboardEntry> entries, uint sequenceNumber) : IDisposable
{
    public IReadOnlyList<ClipboardEntry> Entries { get; } = entries;

    /// <summary>Gets the change counter captured with these contents while the clipboard was still open.</summary>
    public uint SequenceNumber { get; } = sequenceNumber;

    public bool IsEmpty => Entries.Count == 0;

    /// <summary>Puts these contents back on the clipboard, unless <paramref name="stillOurs"/> says someone else wrote to it.</summary>
    public ClipboardRestore Restore(Func<uint, bool>? stillOurs = null) => Clipboard.Restore(this, stillOurs);

    public void Dispose()
    {
        foreach (ClipboardEntry entry in Entries)
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(entry.Data);
        }
    }
}
