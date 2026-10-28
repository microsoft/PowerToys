// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// Tracks the clipboard this app borrowed for a paste and decides whether it is still ours to hand back:
/// if the user copied something during the paste, restoring the snapshot would destroy it. Compares
/// <c>GetClipboardSequenceNumber</c> across the borrow; free of Win32 so the rule is testable.
/// </summary>
internal sealed class ClipboardLoan
{
    private uint _expected;

    /// <param name="sequenceAfterWrite">
    /// The clipboard sequence number immediately after our own write landed. Everything after this
    /// point that changes the number was somebody else.
    /// </param>
    public ClipboardLoan(uint sequenceAfterWrite) => _expected = sequenceAfterWrite;

    /// <summary>Gets the sequence number this loan currently expects to see.</summary>
    public uint Expected => _expected;

    /// <summary>
    /// True when the clipboard still holds our write, so the snapshot may be put back; otherwise theirs wins.
    /// </summary>
    public bool MayRestore(uint sequenceNow) => sequenceNow == _expected;

    /// <summary>
    /// Records that <em>we</em> changed the clipboard again, so a retry after a failed restore that moved the
    /// sequence number doesn't mistake our own change for a new owner and abandon the user's data.
    /// </summary>
    public void Rearm(uint sequenceAfterOurChange) => _expected = sequenceAfterOurChange;
}
