// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.CmdPal.Ext.TextExpander.Insertion;

/// <summary>
/// Waits for the app to take a paste before the user's clipboard goes back: handed back too soon, the app would paste
/// what the user copied instead of the snippet. These are Text Expander's numbers and steps
/// (<c>Injector.WaitForPasteToSettle</c>), for a Ctrl+V sent in one batch.
/// </summary>
internal static class PasteSettle
{
    /// <summary>How long after the V an app that answers messages has to read the clipboard, if its read isn't seen.</summary>
    internal const int SettleMs = 350;

    /// <summary>The shortest wait, however quickly the read is seen.</summary>
    internal const int FloorMs = 150;

    /// <summary>A pause after the app lets go of the clipboard, for one that opens it again to read another format.</summary>
    internal const int GraceMs = 20;

    /// <summary>How long a busy app may keep a paste waiting before its clipboard stays on the snippet.</summary>
    internal const int BusyCapMs = 5000;

    internal const int PollMs = 5;

    /// <summary>
    /// Returns true once the clipboard may go back: the app was seen opening and closing it, or it answered messages
    /// for <see cref="SettleMs"/> without being seen. Returns false when it still wasn't answering after
    /// <see cref="BusyCapMs"/>: its Ctrl+V is still queued, so the snippet stays on the clipboard.
    /// </summary>
    /// <param name="isOpenByTarget">Whether the app's process has the clipboard open now.</param>
    /// <param name="isTakingMessages">Whether the app answers messages.</param>
    /// <param name="clock">The clock, read as milliseconds since the V was sent.</param>
    /// <param name="sentAt">The clock's reading when the V was sent.</param>
    public static bool WaitForRead(Func<bool> isOpenByTarget, Func<bool> isTakingMessages, IInsertionClock clock, long sentAt)
    {
        ArgumentNullException.ThrowIfNull(isOpenByTarget);
        ArgumentNullException.ThrowIfNull(isTakingMessages);
        ArgumentNullException.ThrowIfNull(clock);

        long readBy = SettleMs;
        var readSeen = false;
        var wasBusy = false;

        while (true)
        {
            var waited = clock.ElapsedMilliseconds - sentAt;
            if (isOpenByTarget())
            {
                readSeen = true;
            }
            else if (readSeen && waited >= FloorMs)
            {
                // Opened and closed again: the app has the text.
                clock.Sleep(GraceMs);
                return true;
            }

            // Not seen reading by now. An app answering messages had its chance; a busy one still has the Ctrl+V
            // queued, and gets the floor again once it answers.
            if (waited >= readBy)
            {
                if (isTakingMessages())
                {
                    if (!wasBusy)
                    {
                        return true;
                    }

                    wasBusy = false;
                    readBy = waited + FloorMs;
                }
                else if (waited >= BusyCapMs)
                {
                    return false;
                }
                else
                {
                    wasBusy = true;
                }
            }

            clock.Sleep(PollMs);
        }
    }
}
