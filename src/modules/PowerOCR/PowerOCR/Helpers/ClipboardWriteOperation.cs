// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace PowerOCR.Helpers;

/// <summary>
/// Writes once, then retries clipboard persistence when OpenClipboard is temporarily unavailable.
/// Delegates keep the retry policy testable without accessing the system clipboard.
/// </summary>
internal static class ClipboardWriteOperation
{
    private const int CannotOpenClipboard = unchecked((int)0x800401D0);
    private const int MaximumFlushAttempts = 5;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(50);

    internal static async Task<int> ExecuteAsync(
        Action setContent,
        Action flush,
        CancellationToken cancellationToken,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        ArgumentNullException.ThrowIfNull(setContent);
        ArgumentNullException.ThrowIfNull(flush);
        delay ??= Task.Delay;

        cancellationToken.ThrowIfCancellationRequested();
        setContent();

        // SetContent already succeeded. Repeating it can trigger clipboard listeners again
        // and overwrite content copied by another application while we were waiting.
        for (int attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                flush();
                return attempt;
            }
            catch (COMException exception) when (exception.HResult == CannotOpenClipboard && attempt < MaximumFlushAttempts)
            {
                // WinRT clipboard calls must stay on the caller's UI/STA context. Yield
                // between retries so Escape can cancel the session without blocking input.
                await delay(RetryDelay, cancellationToken).ConfigureAwait(true);
            }
        }
    }
}
