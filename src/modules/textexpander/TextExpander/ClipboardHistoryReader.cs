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
/// Reads the text of recent copies from Windows clipboard history (Windows logo key + V) for a snippet that picks a copy
/// when it runs, as {{clipboard:pick}} does. Windows lets the engine read it while the user's app is in front, so it's read
/// before any window opens. What's read is used for that one expansion only: it's never logged or kept.
/// </summary>
internal static class ClipboardHistoryReader
{
    /// <summary>The most copies offered, as many as Windows keeps.</summary>
    internal const int MaxCopies = 25;

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Gets the text copies in clipboard history, newest first and each different one once, or none when history is off,
    /// can't be read within 2 seconds, or holds no text.
    /// </summary>
    public static IReadOnlyList<string> ReadTexts()
    {
        try
        {
            if (!WinClipboard.IsHistoryEnabled())
            {
                return Array.Empty<string>();
            }

            Task<IReadOnlyList<string>> read = Task.Run(ReadAsync);
            return read.Wait(Timeout) ? read.Result : Array.Empty<string>();
        }
        catch (Exception ex) when (ex is COMException or UnauthorizedAccessException or InvalidOperationException or AggregateException)
        {
            Trace.TraceWarning($"Text Expander couldn't read clipboard history (0x{ex.HResult:X8}), so nothing was inserted from it.");
            return Array.Empty<string>();
        }
    }

    private static async Task<IReadOnlyList<string>> ReadAsync()
    {
        var result = await WinClipboard.GetHistoryItemsAsync();
        if (result.Status != WinClipboardHistoryItemsResultStatus.Success)
        {
            return Array.Empty<string>();
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

        return texts;
    }
}
