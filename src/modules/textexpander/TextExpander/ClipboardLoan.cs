// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// Tracks the clipboard this app borrowed for a paste, and decides whether it is still ours to
/// hand back.
///
/// <para>
/// Pasting takes a snapshot of the user's clipboard, replaces it with the expansion, sends
/// Ctrl+V, waits for the target to read it, then republishes the snapshot. That last step empties
/// the clipboard first. If the user copied something during the wait — a few hundred milliseconds
/// in which they are very much still using their computer — restoring unconditionally throws
/// their new copy away and puts stale content back in its place. No error, no undo.
/// </para>
///
/// <para>
/// <c>GetClipboardSequenceNumber</c> increments on every change by anyone, so comparing it across
/// the borrow says exactly whether we are still the last writer.
/// </para>
///
/// <para>
/// Kept free of Win32 so the rule itself — including the awkward part, re-arming after a restore
/// attempt that changed the clipboard before failing — is testable rather than only reachable
/// through a live clipboard.
/// </para>
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
    /// True when the clipboard still holds our write, so the user's snapshot may be put back.
    /// False means somebody else has written since, and their data is newer than anything we are
    /// holding — theirs wins.
    /// </summary>
    public bool MayRestore(uint sequenceNow) => sequenceNow == _expected;

    /// <summary>
    /// Records that <em>we</em> changed the clipboard again, so the next check compares against
    /// the right number.
    ///
    /// <para>This matters on the retry path. A restore that empties the clipboard and then fails
    /// to republish every format has moved the sequence number itself; without re-arming, the
    /// retry would see the mismatch it caused, conclude somebody else owns the clipboard, and
    /// abandon the user's data at the worst possible moment — right after emptying it.</para>
    /// </summary>
    public void Rearm(uint sequenceAfterOurChange) => _expected = sequenceAfterOurChange;
}
