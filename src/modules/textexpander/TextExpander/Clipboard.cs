// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// Minimal clipboard access for the paste-based injection path.
///
/// The clipboard is a shared, single-owner resource that other processes open constantly, so
/// every operation retries briefly rather than failing the whole expansion on one lost race.
///
/// Pasting borrows the clipboard from the user, which means it has to be given back exactly as
/// it was found. Everything here is arranged around that: the clipboard is only ever emptied
/// once we know we can write to it, a snapshot copies every format rather than just the text,
/// and anything that cannot be reproduced makes the whole operation refuse rather than proceed.
/// </summary>
internal static class Clipboard
{
    private const int OpenAttempts = 10;
    private const int OpenRetryDelayMs = 20;

    /// <summary>
    /// Refuse to take over a clipboard larger than this rather than destroy contents we cannot
    /// put back. The expansion still happens, just by typing instead of pasting.
    /// </summary>
    private const long MaxSnapshotBytes = 32L * 1024 * 1024;

    /// <summary>
    /// Gets or sets window that owns the clipboard while we hold it, set by Program to its hidden message
    /// window. That window is on the message-pump thread on purpose, because owning the
    /// clipboard means receiving WM_DESTROYCLIPBOARD when the next application takes it: a
    /// window on the expansion worker, which does not pump, would stall whichever application
    /// copies next until its send times out.
    ///
    /// Writing therefore depends on opening the clipboard against a window belonging to another
    /// thread. Should Windows decline that, <see cref="TryOpen"/> reports
    /// <see cref="ClipboardOpen.Unowned"/>, the write path refuses without emptying anything,
    /// and expansion falls back to typing. Degraded, never destructive.
    /// </summary>
    public static IntPtr OwnerWindow { get; set; } = IntPtr.Zero;

    /// <summary>
    /// Opens the clipboard, reporting whether the result may be written to. Both an owned and an
    /// unowned open are attempted on each pass because the usual reason either fails is that
    /// another process holds the clipboard for a moment, which clears on its own.
    /// </summary>
    private static ClipboardOpen TryOpen()
    {
        for (int attempt = 0; attempt < OpenAttempts; attempt++)
        {
            if (OwnerWindow != IntPtr.Zero && Native.OpenClipboard(OwnerWindow))
            {
                return Record(ClipboardOpen.Owned);
            }

            if (Native.OpenClipboard(IntPtr.Zero))
            {
                return Record(ClipboardOpen.Unowned);
            }

            Thread.Sleep(OpenRetryDelayMs);
        }

        return Record(ClipboardOpen.Failed);
    }

    /// <summary>
    /// Counts the open so the active path is visible without a debugger. Whether opens are ever
    /// <see cref="ClipboardOpen.Owned"/> is the single most telling number here: if they never
    /// are, the paste path is inoperative and expansion has silently degraded to typing.
    /// </summary>
    private static ClipboardOpen Record(ClipboardOpen open)
    {
        InjectionDiagnostics.RecordClipboardOpen(open);
        return open;
    }

    /// <summary>
    /// True when the clipboard holds exactly this text. Used to confirm a takeover actually
    /// happened before Ctrl+V is sent: pasting on the strength of an unverified write is how
    /// the *previous* clipboard contents end up in the user's document.
    /// </summary>
    public static bool HoldsText(string text)
        => string.Equals(GetText(), text, StringComparison.Ordinal);

    /// <summary>Reads the clipboard's Unicode text, or null when it holds something else.</summary>
    public static string? GetText()
    {
        if (!Native.IsClipboardFormatAvailable(ClipboardFormats.CF_UNICODETEXT))
        {
            return null;
        }

        if (TryOpen() == ClipboardOpen.Failed)
        {
            return null;
        }

        try
        {
            IntPtr handle = Native.GetClipboardData(ClipboardFormats.CF_UNICODETEXT);
            if (handle == IntPtr.Zero)
            {
                return null;
            }

            IntPtr pointer = Native.GlobalLock(handle);
            if (pointer == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                return Marshal.PtrToStringUni(pointer);
            }
            finally
            {
                Native.GlobalUnlock(handle);
            }
        }
        finally
        {
            Native.CloseClipboard();
        }
    }

    /// <summary>
    /// Places Unicode text on the clipboard, replacing whatever was there. Refuses outright,
    /// without emptying anything, when the clipboard cannot be written to.
    ///
    /// <para>
    /// The expansion is published alongside the clipboard-history and cloud-clipboard exclusion
    /// formats. Without them, every expansion this app pastes becomes a Clipboard History entry
    /// and is uploaded to the user's Microsoft account for their other devices — for content the
    /// user never chose to copy, and which is taken off the clipboard again moments later. The
    /// exclusions are conventions rather than enforcement; see <see cref="ClipboardPrivacy"/>.
    /// </para>
    /// </summary>
    public static ClipboardWrite SetText(string text)
    {
        ClipboardOpen open = TryOpen();
        if (open == ClipboardOpen.Failed)
        {
            return ClipboardWrite.Refused;
        }

        try
        {
            // Checked before EmptyClipboard, never after: an unowned handle can empty the
            // clipboard perfectly well and then fail every write, which would leave the user
            // with nothing at all.
            if (open != ClipboardOpen.Owned)
            {
                return ClipboardWrite.Refused;
            }

            if (!Native.EmptyClipboard())
            {
                return ClipboardWrite.Refused;
            }

            // Published before the text, so there is no window in which the content is on the
            // clipboard without the markings that say not to keep it.
            PublishPrivacyExclusions();

            byte[] bytes = Encoding.Unicode.GetBytes(text + '\0');
            return Publish(ClipboardFormats.CF_UNICODETEXT, bytes)
                ? ClipboardWrite.Written
                : ClipboardWrite.Failed;
        }
        finally
        {
            Native.CloseClipboard();
        }
    }

    /// <summary>
    /// Marks the already-open clipboard as content that history and cloud sync should skip.
    ///
    /// Best-effort by design: a format that will not register, or will not publish, is a weaker
    /// privacy posture but not a reason to abandon an expansion the user asked for. The paste
    /// itself still verifies what landed on the clipboard before sending Ctrl+V.
    /// </summary>
    private static void PublishPrivacyExclusions()
    {
        foreach (string name in ClipboardPrivacy.FormatNames)
        {
            uint format = RegisteredFormat(name);
            if (format != 0)
            {
                Publish(format, ClipboardPrivacy.DenyPayload());
            }
        }
    }

    /// <summary>
    /// The numeric id of a named clipboard format, registered once and cached. Registration is
    /// idempotent and process-wide, but it is still a call into user32 on a path that runs per
    /// expansion.
    /// </summary>
    private static uint RegisteredFormat(string name)
    {
        if (_registeredFormats.TryGetValue(name, out uint cached))
        {
            return cached;
        }

        uint id = Native.RegisterClipboardFormatW(name);
        _registeredFormats[name] = id;
        return id;
    }

    private static readonly Dictionary<string, uint> _registeredFormats = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the clipboard's change counter. Incremented by any process writing to the clipboard, so a
    /// difference across a borrow means somebody else owns what is there now.
    /// </summary>
    public static uint SequenceNumber => Native.GetClipboardSequenceNumber();

    /// <summary>
    /// True while <paramref name="targetWindow"/>'s own process holds the clipboard open — which
    /// is what an application does while it reads a paste.
    ///
    /// <para>
    /// Attributed to the target's process rather than answering "is anyone reading?". Our own
    /// write raises <c>WM_CLIPBOARDUPDATE</c>, and every clipboard history tool, remote-desktop
    /// agent and format listener on the machine answers it by opening the clipboard and
    /// enumerating formats, including the exclusion formats published with the expansion.
    /// Treating one of those as the target's read would let the restore run before the paste had
    /// landed, which would put the user's previous clipboard into their document.
    /// </para>
    /// </summary>
    public static bool IsOpenByTarget(IntPtr targetWindow)
    {
        if (targetWindow == IntPtr.Zero)
        {
            return false;
        }

        IntPtr open = Native.GetOpenClipboardWindow();

        // Zero means nobody has it open, or somebody opened it with a null owner — which cannot
        // be attributed to anyone, so it is treated as not-the-target and the full settle is paid.
        if (open == IntPtr.Zero || open == OwnerWindow)
        {
            return false;
        }

        _ = Native.GetWindowThreadProcessId(open, out uint openProcess);
        _ = Native.GetWindowThreadProcessId(targetWindow, out uint targetProcess);
        return openProcess != 0 && openProcess == targetProcess;
    }

    /// <summary>
    /// Copies everything currently on the clipboard so it can be restored after a paste.
    ///
    /// Returns null when the clipboard cannot be reproduced — unreadable, too large, or holding
    /// a format that is a handle rather than memory. That is a refusal rather than a failure:
    /// the caller leaves the clipboard alone and types instead. Returning a partial snapshot
    /// would be far worse than returning nothing, because restoring empties the clipboard first
    /// and would drop whatever the snapshot had quietly skipped.
    /// </summary>
    public static ClipboardSnapshot? Snapshot()
    {
        if (TryOpen() == ClipboardOpen.Failed)
        {
            return null;
        }

        try
        {
            IReadOnlyList<uint>? plan = PlanCapture();
            if (plan is null)
            {
                return null;
            }

            var entries = new List<ClipboardEntry>(plan.Count);
            long total = 0;

            foreach (uint format in plan)
            {
                // A format that was advertised and then will not produce data cannot be put
                // back, so the clipboard is left alone rather than partly restored. This covers
                // delay-rendered content and the OLE stream and storage mediums, where the
                // memory-backed descriptor is readable but the content behind it is not.
                IntPtr handle = Native.GetClipboardData(format);
                if (handle == IntPtr.Zero)
                {
                    return null;
                }

                ulong size = Native.GlobalSize(handle).ToUInt64();
                if (size == 0 || size > int.MaxValue)
                {
                    return null;
                }

                total += (long)size;
                if (total > MaxSnapshotBytes)
                {
                    return null;
                }

                IntPtr pointer = Native.GlobalLock(handle);
                if (pointer == IntPtr.Zero)
                {
                    return null;
                }

                try
                {
                    var bytes = new byte[size];
                    Marshal.Copy(pointer, bytes, 0, bytes.Length);
                    entries.Add(new ClipboardEntry(format, bytes));
                }
                finally
                {
                    Native.GlobalUnlock(handle);
                }
            }

            return new ClipboardSnapshot(entries);
        }
        finally
        {
            Native.CloseClipboard();
        }
    }

    /// <summary>
    /// Lists what to copy off an already-open clipboard, or null when it cannot be reproduced.
    /// </summary>
    private static IReadOnlyList<uint>? PlanCapture()
    {
        // Two passes on purpose. Fetching a format can make its owner render it, which runs that
        // application's code and can change the clipboard underneath an enumeration still in
        // progress, so the list is taken first and read second. Nothing in this loop may call
        // into Win32, because the last error is what says whether the list is complete.
        var formats = new List<uint>();
        uint format = 0;
        while ((format = Native.EnumClipboardFormats(format)) != 0)
        {
            formats.Add(format);
        }

        // Zero means end-of-list *or* failure, told apart only by the last error. A failure half
        // way through yields a short list that would otherwise look like a whole clipboard, and
        // restoring it would republish the prefix and drop the rest.
        if (Marshal.GetLastWin32Error() != Native.ERROR_SUCCESS)
        {
            return null;
        }

        return ClipboardFormats.PlanCapture(formats);
    }

    /// <summary>
    /// Puts a snapshot back, emptying the clipboard when the snapshot is empty. Returns false if
    /// the clipboard was not fully restored, which the caller is expected to act on rather than
    /// discard: a partial restore leaves the user worse off than no restore at all.
    ///
    /// <para>
    /// The snapshot carries every format the clipboard held, including the history and cloud
    /// exclusion formats, so content the user's password manager marked as protected goes back
    /// still marked. Republishing it as an ordinary, unmarked entry would strip a protection its
    /// owner deliberately asked for.
    /// </para>
    /// </summary>
    public static bool Restore(ClipboardSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        ClipboardOpen open = TryOpen();
        if (open == ClipboardOpen.Failed)
        {
            return false;
        }

        try
        {
            // As in SetText: an unowned handle would empty the clipboard and then fail to
            // republish anything, turning a restore into a wipe.
            if (open != ClipboardOpen.Owned)
            {
                return false;
            }

            if (!Native.EmptyClipboard())
            {
                return false;
            }

            bool allPublished = true;
            foreach (ClipboardEntry entry in snapshot.Entries)
            {
                // One format failing must not abandon the rest: restoring an image without its
                // accompanying metadata beats restoring nothing at all.
                if (!Publish(entry.Format, entry.Data))
                {
                    allPublished = false;
                }
            }

            return allPublished;
        }
        finally
        {
            Native.CloseClipboard();
        }
    }

    /// <summary>
    /// Hands a copy of <paramref name="data"/> to the already-open clipboard. The clipboard takes
    /// ownership of the block on success, so it must only be freed on the failure path.
    /// </summary>
    private static bool Publish(uint format, byte[] data)
    {
        IntPtr handle = Native.GlobalAlloc(Native.GMEM_MOVEABLE, (UIntPtr)(uint)data.Length);
        if (handle == IntPtr.Zero)
        {
            return false;
        }

        IntPtr pointer = Native.GlobalLock(handle);
        if (pointer == IntPtr.Zero)
        {
            Native.GlobalFree(handle);
            return false;
        }

        try
        {
            Marshal.Copy(data, 0, pointer, data.Length);
        }
        finally
        {
            Native.GlobalUnlock(handle);
        }

        if (Native.SetClipboardData(format, handle) == IntPtr.Zero)
        {
            Native.GlobalFree(handle);
            return false;
        }

        return true;
    }
}
