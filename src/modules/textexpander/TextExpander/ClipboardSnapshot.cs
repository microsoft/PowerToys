// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// An off-clipboard copy of the user's clipboard contents, taken before an expansion pastes over
/// them. An empty snapshot means the clipboard was genuinely empty — a clipboard holding
/// anything that could not be copied refuses in <see cref="Clipboard.Snapshot"/> rather than
/// arriving here looking empty — so restoring one clears the clipboard instead of leaving the
/// expansion's text behind.
/// </summary>
internal sealed class ClipboardSnapshot(IReadOnlyList<ClipboardEntry> entries)
{
    public IReadOnlyList<ClipboardEntry> Entries { get; } = entries;

    public bool IsEmpty => Entries.Count == 0;

    /// <summary>Puts these contents back on the clipboard.</summary>
    public bool Restore() => Clipboard.Restore(this);
}
