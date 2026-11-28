// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Microsoft.PowerToys.TextExpander;

/// <summary>Reads and selects logical UTF-16 ranges in the standard Windows Edit control, on the expansion worker.</summary>
internal sealed class NativeEdit(Func<NativeEdit.Snapshot?> read, Func<int, int, bool> select, Func<bool> eraseSelection, bool singleLineOnly)
{
    private const int MessageTimeoutMs = 50;
    private const int MaxTextLength = 256 * 1024;
    private const uint WmGetText = 0x000D;
    private const uint WmGetTextLength = 0x000E;
    private const uint EmGetSel = 0x00B0;
    private const uint EmSetSel = 0x00B1;
    private const uint EmReplaceSel = 0x00C2;

    internal readonly record struct Snapshot(string Text, int SelectionStart, int SelectionEnd)
    {
        internal bool HasCaretAt(int offset) => SelectionStart == offset && SelectionEnd == offset;
    }

    internal sealed record Undo(int Start, int Caret, string Text, byte[] DocumentHash);

    internal sealed record Operation(NativeEdit Editor, string TypedPrefix, string CaretSuffix, Undo? Undo, Action<Undo>? RecordUndo);

    // Rich Edit can return paragraph breaks in a different representation than it uses for selection offsets.
    // Only use its logical ranges for a single-line document. Other Rich Edit documents keep ordinary insertion.
    internal bool SingleLineOnly => singleLineOnly;

    internal Snapshot? Read()
    {
        Snapshot? snapshot = read();
        return singleLineOnly && snapshot is { } current && current.Text.AsSpan().ContainsAny('\r', '\n') ? null : snapshot;
    }

    internal bool Select(int start, int end) => select(start, end);

    internal bool EraseSelection() => eraseSelection();

    internal static NativeEdit? ForWindow(IntPtr window)
    {
        if (window == IntPtr.Zero || !Native.IsWindowUnicode(window))
        {
            return null;
        }

        char[] className = new char[64];
        int length = Native.GetClassNameW(window, className, className.Length);
        ReadOnlySpan<char> name = className.AsSpan(0, length);
        bool plainEdit = name.Equals("Edit", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("WindowsForms10.EDIT.", StringComparison.OrdinalIgnoreCase);
        bool richEdit = name.StartsWith("RichEdit", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("WindowsForms10.RichEdit", StringComparison.OrdinalIgnoreCase);
        if (!plainEdit && !richEdit)
        {
            return null;
        }

        return new(
            () => Read(window),
            (start, end) => Request(window, EmSetSel, new IntPtr(start), new IntPtr(end), out _),
            () => EraseSelection(window),
            singleLineOnly: richEdit);
    }

    // Classic multiline Edit returns CRLF even when we generated the line break with Enter instead of pasting it.
    internal static string WithEditLineBreaks(string text)
        => text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "\r\n", StringComparison.Ordinal);

    internal static byte[] Hash(string text) => SHA256.HashData(MemoryMarshal.AsBytes(text.AsSpan()));

    private static unsafe bool EraseSelection(IntPtr window)
    {
        char terminator = '\0';
        return Request(window, EmReplaceSel, new IntPtr(1), (IntPtr)(&terminator), out _);
    }

    private static unsafe Snapshot? Read(IntPtr window)
    {
        if ((Native.GetWindowLongW(window, Native.GWL_STYLE) & Native.ES_PASSWORD) != 0
            || !Request(window, WmGetTextLength, IntPtr.Zero, IntPtr.Zero, out IntPtr size)
            || size.ToInt64() is < 0 or > MaxTextLength)
        {
            return null;
        }

        int start = -1;
        int end = -1;
        if (!Request(window, EmGetSel, (IntPtr)(&start), (IntPtr)(&end), out _))
        {
            return null;
        }

        // One extra character detects growth between the length query and the read instead of accepting a truncated prefix.
        char[] buffer = new char[size.ToInt32() + 2];
        try
        {
            fixed (char* output = buffer)
            {
                if (!Request(window, WmGetText, new IntPtr(buffer.Length), (IntPtr)output, out IntPtr readLength)
                    || readLength.ToInt64() != size.ToInt64()
                    || start < 0 || end < start || end > readLength.ToInt32())
                {
                    return null;
                }

                return new(new string(buffer, 0, readLength.ToInt32()), start, end);
            }
        }
        finally
        {
            Array.Clear(buffer);
        }
    }

    private static bool Request(IntPtr window, uint message, IntPtr wParam, IntPtr lParam, out IntPtr result)
        => Native.SendMessageTimeoutW(window, message, wParam, lParam, Native.SMTO_BLOCK | Native.SMTO_ABORTIFHUNG | Native.SMTO_ERRORONEXIT, MessageTimeoutMs, out result) != IntPtr.Zero;
}
