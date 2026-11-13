// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// Hands the user's clipboard back after a paste, for Text Expander and its Command Palette extension alike, so the two
/// can't drift apart. Free of Win32: the restore, the clipboard's sequence number and the sleep are passed in.
/// </summary>
internal static class ClipboardHandBack
{
    /// <summary>
    /// Tries up to <paramref name="attempts"/> times, <paramref name="retryDelayMs"/> apart, to put the user's clipboard
    /// back with <paramref name="restore"/>, trying again while another app holds it. Someone else's newer copy wins, and
    /// that's a skip, not a loss. Whether the clipboard is still ours is asked before each attempt, and by
    /// <paramref name="restore"/> again once it holds the clipboard open, before emptying it, since the open may have
    /// waited for an app that was copying. Returns <see cref="ClipboardRestore.Restored"/>,
    /// <see cref="ClipboardRestore.Superseded"/>, or the last attempt's failure.
    /// </summary>
    /// <param name="stillOurs">Whether our window still owns the clipboard, as emptying it made it.</param>
    public static ClipboardRestore Run(Func<Func<uint, bool>, ClipboardRestore> restore, ClipboardLoan loan, Func<uint> sequence, Func<bool> stillOurs, Action<int> sleep, int attempts, int retryDelayMs)
    {
        ArgumentNullException.ThrowIfNull(restore);
        ArgumentNullException.ThrowIfNull(loan);
        ArgumentNullException.ThrowIfNull(sequence);
        ArgumentNullException.ThrowIfNull(stillOurs);
        ArgumentNullException.ThrowIfNull(sleep);

        ClipboardRestore result = ClipboardRestore.Untouched;
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            if (!loan.MayRestore(sequence()))
            {
                return ClipboardRestore.Superseded;
            }

            result = restore(loan.MayRestore);
            if (result is ClipboardRestore.Restored or ClipboardRestore.Superseded)
            {
                return result;
            }

            // Only an attempt that emptied the clipboard changed it: re-armed then, so the retry doesn't take our own
            // change for someone else's. One that couldn't open the clipboard changed nothing, so any change since is
            // someone else's, such as a copy made while it waited, and must not be adopted. Nor may a copy made once the
            // attempt closed the clipboard: the number is read, then whether the clipboard is still ours, as when the
            // loan was taken (see ClipboardLoan.TakeAfterWrite).
            if (result == ClipboardRestore.Incomplete)
            {
                uint sequenceAfterOurChange = sequence();
                if (!stillOurs())
                {
                    return ClipboardRestore.Superseded;
                }

                loan.Rearm(sequenceAfterOurChange);
            }

            if (attempt + 1 < attempts && retryDelayMs > 0)
            {
                sleep(retryDelayMs);
            }
        }

        return result;
    }
}
