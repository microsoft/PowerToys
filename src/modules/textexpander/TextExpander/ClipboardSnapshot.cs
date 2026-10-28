// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// An off-clipboard copy of the user's clipboard, taken before an expansion pastes over it. An empty snapshot
/// means the clipboard was genuinely empty, so restoring it clears the clipboard.
/// </summary>
internal sealed class ClipboardSnapshot(IReadOnlyList<ClipboardEntry> entries) : IDisposable
{
    public IReadOnlyList<ClipboardEntry> Entries { get; } = entries;

    public bool IsEmpty => Entries.Count == 0;

    /// <summary>Puts these contents back on the clipboard.</summary>
    public bool Restore() => Clipboard.Restore(this);

    public void Dispose()
    {
        foreach (ClipboardEntry entry in Entries)
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(entry.Data);
        }
    }
}
