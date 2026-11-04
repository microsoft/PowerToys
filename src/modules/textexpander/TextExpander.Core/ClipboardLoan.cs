// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

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

    /// <summary>
    /// Takes the loan right after our own write: the clipboard's sequence number, then whether the clipboard is still
    /// ours. Asked in that order, a copy someone made in between can't pass for ours, since emptying the clipboard to
    /// write it made them its owner: there's no loan then, and nothing may be put back over their copy. The number can't
    /// be taken while the clipboard is still open: closing it adds the formats Windows makes from the text, which move
    /// the number on.
    /// </summary>
    /// <param name="sequence">Reads the clipboard's sequence number.</param>
    /// <param name="stillOurs">Whether our window still owns the clipboard.</param>
    public static ClipboardLoan? TakeAfterWrite(Func<uint> sequence, Func<bool> stillOurs)
    {
        ArgumentNullException.ThrowIfNull(sequence);
        ArgumentNullException.ThrowIfNull(stillOurs);

        uint sequenceAfterWrite = sequence();
        return stillOurs() ? new ClipboardLoan(sequenceAfterWrite) : null;
    }

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
