// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// Minimal clipboard access for the paste-based injection path. Every operation retries briefly, and the
/// user's clipboard is borrowed and given back exactly: it is emptied only once we can write to it, a
/// snapshot copies every format, and anything that cannot be reproduced makes the operation refuse.
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
    /// Gets or sets the window that owns the clipboard while we hold it: Program's hidden message window on the
    /// message-pump thread, because the owner receives WM_DESTROYCLIPBOARD and a non-pumping window would stall
    /// the next app that copies. If Windows declines an owned open, <see cref="TryOpen"/> reports
    /// <see cref="ClipboardOpen.Unowned"/> and the write path refuses without emptying anything.
    /// </summary>
    public static IntPtr OwnerWindow { get; set; } = IntPtr.Zero;

    /// <summary>
    /// Opens the clipboard, reporting whether the result may be written to. Both opens are retried because
    /// another process usually holds the clipboard only for a moment.
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
    /// Counts the open for diagnostics; if opens are never <see cref="ClipboardOpen.Owned"/>, pasting has degraded to typing.
    /// </summary>
    private static ClipboardOpen Record(ClipboardOpen open)
    {
        InjectionDiagnostics.RecordClipboardOpen(open);
        return open;
    }

    /// <summary>
    /// True when the clipboard holds exactly this text, confirming a takeover before Ctrl+V is sent.
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
    /// Places Unicode text on the clipboard, refusing without emptying anything when it can't be written to.
    /// The text is published with the clipboard-history and cloud-clipboard exclusion formats so expansions
    /// don't become history entries or sync to other devices (see <see cref="ClipboardPrivacy"/>).
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
            // Checked before EmptyClipboard: an unowned handle can empty the clipboard and then fail every write.
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
            try
            {
                return Publish(ClipboardFormats.CF_UNICODETEXT, bytes)
                    ? ClipboardWrite.Written
                    : ClipboardWrite.Failed;
            }
            finally
            {
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes);
            }
        }
        finally
        {
            Native.CloseClipboard();
        }
    }

    /// <summary>
    /// Marks the already-open clipboard as content that history and cloud sync should skip. Best-effort:
    /// a format that won't register or publish doesn't abandon the expansion.
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

    /// <summary>The numeric id of a named clipboard format, registered once and cached.</summary>
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
    /// True while <paramref name="targetWindow"/>'s own process holds the clipboard open, as it does while
    /// reading a paste. Only the target's process counts: clipboard listeners also open the clipboard after
    /// our write, and mistaking one for the target would restore before the paste landed.
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
    /// Copies everything on the clipboard so it can be restored after a paste. Returns null, a refusal, when
    /// the clipboard can't be fully reproduced (unreadable, too large, or a handle format): a partial snapshot
    /// would drop whatever it skipped when restoring.
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
                // An advertised format that produces no data (delay-rendered, OLE stream or storage)
                // can't be put back, so the clipboard is left alone.
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
        // Listed first and read second, because fetching a format can run its owner's rendering code.
        // Nothing in this loop may call into Win32: the last error says whether the list is complete.
        var formats = new List<uint>();
        uint format = 0;
        while ((format = Native.EnumClipboardFormats(format)) != 0)
        {
            formats.Add(format);
        }

        // Zero means end-of-list or failure; a failure would otherwise pass for a complete, shorter list.
        if (Marshal.GetLastWin32Error() != Native.ERROR_SUCCESS)
        {
            return null;
        }

        return ClipboardFormats.PlanCapture(formats);
    }

    /// <summary>
    /// Puts a snapshot back, emptying the clipboard when the snapshot is empty. Returns false unless fully
    /// restored. Every format goes back, including exclusion formats, so protected content stays protected.
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
