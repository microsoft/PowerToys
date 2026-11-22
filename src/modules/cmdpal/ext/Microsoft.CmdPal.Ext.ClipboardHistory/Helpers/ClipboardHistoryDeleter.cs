// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.ApplicationModel.DataTransfer;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.Helpers;

/// <summary>
/// Removes an item from clipboard history after a paste, without racing the target app's clipboard read.
/// </summary>
internal static class ClipboardHistoryDeleter
{
    internal const int ClipboardCantOpenHResult = unchecked((int)0x800401D0); // CLIPBRD_E_CANT_OPEN

    internal static readonly TimeSpan DefaultDelayAfterPaste = TimeSpan.FromMilliseconds(400);
    internal static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromMilliseconds(50);
    internal const int DefaultMaxAttempts = 5;

    /// <summary>
    /// Deletes the item on a background thread once the target app has had time to read the clipboard.
    /// </summary>
    internal static void DeleteAfterPaste(ClipboardHistoryItem item)
    {
        _ = Task.Run(() =>
        {
            Thread.Sleep(DefaultDelayAfterPaste);
            TryDeleteWithRetry(
                () => Clipboard.DeleteItemFromHistory(item),
                Thread.Sleep,
                DefaultMaxAttempts,
                DefaultRetryDelay,
                LogWarning);
        });
    }

    /// <returns><see langword="true"/> if the item was deleted; otherwise <see langword="false"/>. Never throws.</returns>
    internal static bool TryDeleteWithRetry(
        Func<bool> deleteItem,
        Action<TimeSpan> sleep,
        int maxAttempts,
        TimeSpan retryDelay,
        Action<string> logWarning)
    {
        ArgumentNullException.ThrowIfNull(deleteItem);
        ArgumentNullException.ThrowIfNull(sleep);
        ArgumentNullException.ThrowIfNull(logWarning);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                if (deleteItem())
                {
                    return true;
                }

                logWarning("Clipboard history item was not deleted after paste (it may already be gone)");
                return false;
            }
            catch (COMException ex) when (ex.HResult == ClipboardCantOpenHResult && attempt < maxAttempts)
            {
                // Another app (typically the paste target) has the clipboard open; back off and retry.
                sleep(retryDelay * attempt);
            }
            catch (Exception ex)
            {
                logWarning($"Failed to delete clipboard history item after paste (attempt {attempt}/{maxAttempts}): {ex.Message}");
                return false;
            }
        }

        return false;
    }

    private static void LogWarning(string message) =>
        ExtensionHost.LogMessage(new LogMessage { Message = message, State = MessageState.Warning });
}
