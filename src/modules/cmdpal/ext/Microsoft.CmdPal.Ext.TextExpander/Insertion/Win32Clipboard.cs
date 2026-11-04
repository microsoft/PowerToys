// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Microsoft.PowerToys.TextExpander;

namespace Microsoft.CmdPal.Ext.TextExpander.Insertion;

/// <summary>
/// The Windows clipboard, borrowed and given back as Text Expander does (<c>TextExpander\Clipboard.cs</c>), with the rules
/// it shares through TextExpander.Core: <see cref="ClipboardFormats"/> decides what a copy can put back, and
/// <see cref="ClipboardPrivacy"/> which formats keep a write out of Clipboard History.
/// </summary>
internal sealed class Win32Clipboard : IClipboardAccess
{
    private const int OpenAttempts = 10;
    private const int OpenRetryDelayMs = 20;

    /// <summary>Text Expander's limit: a larger clipboard isn't borrowed, and the snippet is typed instead.</summary>
    private const long MaxSavedBytes = 32L * 1024 * 1024;

    private static readonly ConcurrentDictionary<string, uint> RegisteredFormats = new(StringComparer.Ordinal);

    public uint SequenceNumber => NativeMethods.GetClipboardSequenceNumber();

    public bool IsOurs => ClipboardOwnerWindow.Handle != 0 && NativeMethods.GetClipboardOwner() == ClipboardOwnerWindow.Handle;

    public string? ReadText()
    {
        if (!NativeMethods.IsClipboardFormatAvailable(ClipboardFormats.CF_UNICODETEXT) || !TryOpen(out _))
        {
            return null;
        }

        try
        {
            var handle = NativeMethods.GetClipboardData(ClipboardFormats.CF_UNICODETEXT);
            if (handle == 0)
            {
                return null;
            }

            var pointer = NativeMethods.GlobalLock(handle);
            if (pointer == 0)
            {
                return null;
            }

            try
            {
                return Marshal.PtrToStringUni(pointer);
            }
            finally
            {
                NativeMethods.GlobalUnlock(handle);
            }
        }
        finally
        {
            NativeMethods.CloseClipboard();
        }
    }

    public SavedClipboard? Save()
    {
        if (!TryOpen(out _))
        {
            return null;
        }

        try
        {
            // Listed first and read second, because reading a format can run its owner's rendering code.
            var formats = new List<uint>();
            uint format = 0;
            while ((format = NativeMethods.EnumClipboardFormats(format)) != 0)
            {
                formats.Add(format);
            }

            // Zero means the end of the list, or a failure that would otherwise pass for a shorter list.
            if (Marshal.GetLastPInvokeError() != NativeMethods.ERROR_SUCCESS)
            {
                return null;
            }

            var plan = ClipboardFormats.PlanCapture(formats);
            if (plan is null)
            {
                return null;
            }

            var entries = new List<ClipboardEntry>(plan.Count);
            long total = 0;
            foreach (var planned in plan)
            {
                // A format that renders no data can't be put back, so the clipboard is left alone.
                var handle = NativeMethods.GetClipboardData(planned);
                if (handle == 0)
                {
                    return Discard(entries);
                }

                var size = (ulong)NativeMethods.GlobalSize(handle);
                total += (long)Math.Min(size, (ulong)long.MaxValue);
                if (size == 0 || size > int.MaxValue || total > MaxSavedBytes)
                {
                    return Discard(entries);
                }

                var pointer = NativeMethods.GlobalLock(handle);
                if (pointer == 0)
                {
                    return Discard(entries);
                }

                try
                {
                    var bytes = new byte[size];
                    Marshal.Copy(pointer, bytes, 0, bytes.Length);
                    entries.Add(new ClipboardEntry(planned, bytes));
                }
                finally
                {
                    NativeMethods.GlobalUnlock(handle);
                }
            }

            return new SavedClipboard(entries);
        }
        finally
        {
            NativeMethods.CloseClipboard();
        }
    }

    public ClipboardWrite WriteText(string text, bool keepOutOfHistory)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (!TryOpen(out var owned))
        {
            return ClipboardWrite.Refused;
        }

        try
        {
            // Without an owner, EmptyClipboard succeeds and every write after it fails: the clipboard would be wiped.
            if (!owned || !NativeMethods.EmptyClipboard())
            {
                return ClipboardWrite.Refused;
            }

            // Marked before the text goes on, so the text is never on the clipboard without the marks.
            if (keepOutOfHistory)
            {
                PublishPrivacyFormats();
            }

            var bytes = Encoding.Unicode.GetBytes(text + '\0');
            try
            {
                return Publish(ClipboardFormats.CF_UNICODETEXT, bytes) ? ClipboardWrite.Written : ClipboardWrite.Failed;
            }
            finally
            {
                CryptographicOperations.ZeroMemory(bytes);
            }
        }
        finally
        {
            NativeMethods.CloseClipboard();
        }
    }

    public bool HoldsText(string text) => string.Equals(ReadText(), text, StringComparison.Ordinal);

    public ClipboardRestore Restore(SavedClipboard saved, Func<uint, bool> stillOurs)
    {
        ArgumentNullException.ThrowIfNull(saved);
        ArgumentNullException.ThrowIfNull(stillOurs);

        if (!TryOpen(out var owned))
        {
            return ClipboardRestore.Untouched;
        }

        try
        {
            // Asked again now the clipboard is open: the open may have waited for an app that was copying.
            if (!stillOurs(SequenceNumber))
            {
                return ClipboardRestore.Superseded;
            }

            if (!owned || !NativeMethods.EmptyClipboard())
            {
                return ClipboardRestore.Untouched;
            }

            // Windows takes the restore for a new copy: unmarked, it would add the user's last copy to Clipboard History again.
            if (ClipboardPrivacy.ShouldMarkRestore(saved.Entries, RegisteredFormat))
            {
                PublishPrivacyFormats();
            }

            var allPublished = true;
            foreach (var entry in saved.Entries)
            {
                // One format failing doesn't abandon the rest.
                allPublished &= Publish(entry.Format, entry.Data);
            }

            return allPublished ? ClipboardRestore.Restored : ClipboardRestore.Incomplete;
        }
        finally
        {
            NativeMethods.CloseClipboard();
        }
    }

    public uint OpenedByProcessId()
    {
        var window = NativeMethods.GetOpenClipboardWindow();
        if (window == 0 || window == ClipboardOwnerWindow.Handle)
        {
            return 0;
        }

        _ = NativeMethods.GetWindowThreadProcessId(window, out var processId);
        return processId;
    }

    /// <summary>
    /// Opens the clipboard, with the owner window when there is one, retrying while another app holds it for a moment.
    /// <paramref name="owned"/> says whether it may be written to.
    /// </summary>
    private static bool TryOpen(out bool owned)
    {
        var owner = ClipboardOwnerWindow.Handle;
        for (var attempt = 0; attempt < OpenAttempts; attempt++)
        {
            if (owner != 0 && NativeMethods.OpenClipboard(owner))
            {
                owned = true;
                return true;
            }

            if (NativeMethods.OpenClipboard(0))
            {
                owned = false;
                return true;
            }

            Thread.Sleep(OpenRetryDelayMs);
        }

        owned = false;
        return false;
    }

    private static SavedClipboard? Discard(List<ClipboardEntry> entries)
    {
        new SavedClipboard(entries).Dispose();
        return null;
    }

    private static void PublishPrivacyFormats()
    {
        foreach (var name in ClipboardPrivacy.FormatNames)
        {
            var format = RegisteredFormat(name);
            if (format != 0)
            {
                Publish(format, ClipboardPrivacy.DenyPayload());
            }
        }
    }

    private static uint RegisteredFormat(string name) => RegisteredFormats.GetOrAdd(name, static n => NativeMethods.RegisterClipboardFormat(n));

    /// <summary>
    /// Hands a copy of <paramref name="data"/> to the open clipboard, which owns the memory once it takes it.
    /// </summary>
    private static bool Publish(uint format, byte[] data)
    {
        var handle = NativeMethods.GlobalAlloc(NativeMethods.GMEM_MOVEABLE, (nuint)data.Length);
        if (handle == 0)
        {
            return false;
        }

        var pointer = NativeMethods.GlobalLock(handle);
        if (pointer == 0)
        {
            NativeMethods.GlobalFree(handle);
            return false;
        }

        try
        {
            Marshal.Copy(data, 0, pointer, data.Length);
        }
        finally
        {
            NativeMethods.GlobalUnlock(handle);
        }

        if (NativeMethods.SetClipboardData(format, handle) == 0)
        {
            NativeMethods.GlobalFree(handle);
            return false;
        }

        return true;
    }
}
