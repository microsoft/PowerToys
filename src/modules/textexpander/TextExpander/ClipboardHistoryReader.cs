// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using WinClipboard = global::Windows.ApplicationModel.DataTransfer.Clipboard;
using WinClipboardHistoryItemsResultStatus = global::Windows.ApplicationModel.DataTransfer.ClipboardHistoryItemsResultStatus;
using WinStandardDataFormats = global::Windows.ApplicationModel.DataTransfer.StandardDataFormats;

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// Reads the text of recent copies from Windows clipboard history (Windows logo key + V) for a snippet that picks from it,
/// as {{clipboard:pick}} does. Windows lets the engine read it while the user's app is in front, so it's read before any
/// window opens. What's read is used for that one expansion only: it's never logged or kept.
/// </summary>
internal static class ClipboardHistoryReader
{
    /// <summary>The most copies read, as many as Windows keeps.</summary>
    internal const int MaxCopies = 25;

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    // Set only while a read is running, even after its caller times out; never keeps the result.
    private static int _reading;

    /// <summary>
    /// Gets the text copies in clipboard history, newest first and each different one once; or that history is off, or
    /// couldn't be read within 2 seconds.
    /// </summary>
    public static ClipboardHistory Read() => Read(WinClipboard.IsHistoryEnabled, ReadAsync, Timeout);

    /// <summary>
    /// Reads clipboard history with <paramref name="readAsync"/>, one read at a time. A read that timed out may still be
    /// running, as when Windows' clipboard service hangs. Another isn't started next to it, where it would hang too: until
    /// it ends, history can't be read.
    /// </summary>
    internal static ClipboardHistory Read(Func<bool> isEnabled, Func<Task<ClipboardHistory>> readAsync, TimeSpan timeout)
    {
        try
        {
            if (!isEnabled())
            {
                return ClipboardHistory.TurnedOff;
            }

            if (Interlocked.CompareExchange(ref _reading, 1, 0) != 0)
            {
                return ClipboardHistory.NotRead;
            }

            Task<ClipboardHistory> read = Task.Run(async () =>
            {
                try
                {
                    return await readAsync().ConfigureAwait(false);
                }
                finally
                {
                    Volatile.Write(ref _reading, 0);
                }
            });
            return read.Wait(timeout) ? read.Result : ClipboardHistory.NotRead;
        }
        catch (Exception ex) when (ex is COMException or UnauthorizedAccessException or InvalidOperationException or AggregateException)
        {
            Trace.TraceWarning($"Text Expander couldn't read clipboard history (0x{ex.HResult:X8}), so nothing was inserted from it.");
            return ClipboardHistory.NotRead;
        }
    }

    private static async Task<ClipboardHistory> ReadAsync()
    {
        var result = await WinClipboard.GetHistoryItemsAsync();
        switch (result.Status)
        {
            case WinClipboardHistoryItemsResultStatus.Success:
                break;
            case WinClipboardHistoryItemsResultStatus.ClipboardHistoryDisabled:
                return ClipboardHistory.TurnedOff;
            default:
                return ClipboardHistory.NotRead;
        }

        var texts = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in result.Items)
        {
            if (texts.Count >= MaxCopies)
            {
                break;
            }

            if (item.Content.Contains(WinStandardDataFormats.Text))
            {
                string text = await item.Content.GetTextAsync();
                if (!string.IsNullOrEmpty(text) && seen.Add(text))
                {
                    texts.Add(text);
                }
            }
        }

        return new ClipboardHistory(ClipboardHistoryState.Read, texts);
    }
}
