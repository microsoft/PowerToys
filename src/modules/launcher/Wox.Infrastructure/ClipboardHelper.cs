// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

using Wox.Plugin.Logger;

namespace Wox.Infrastructure
{
    public static class ClipboardHelper
    {
        private const uint ErrorCodeClipboardCantOpen = 0x800401D0;
        private const int MaxRetries = 10;
        private const int RetryDelayMs = 10;

        /// <summary>
        /// Copies text to the clipboard with retry logic for when the clipboard is temporarily in use.
        /// </summary>
        /// <param name="text">The text to copy to the clipboard.</param>
        /// <returns><see langword="true"/> if the text was successfully copied; otherwise, <see langword="false"/>.</returns>
        public static bool CopyToClipboard(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
            {
                return TryCopyWithRetry(text);
            }

            var success = false;
            var thread = new Thread(() =>
            {
                success = TryCopyWithRetry(text);
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            return success;
        }

        private static bool TryCopyWithRetry(string text)
        {
            for (var i = 0; i < MaxRetries; i++)
            {
                try
                {
                    Clipboard.SetText(text);
                    return true;
                }
                catch (ExternalException ex) when ((uint)ex.ErrorCode == ErrorCodeClipboardCantOpen)
                {
                    Log.Warn($"Failed to copy to clipboard (attempt {i + 1}/{MaxRetries}): clipboard is currently in use.", typeof(ClipboardHelper));
                }
                catch (Exception ex)
                {
                    Log.Exception("Failed to copy to clipboard", ex, typeof(ClipboardHelper));
                    return false;
                }

                Thread.Sleep(RetryDelayMs);
            }

            Log.Error($"Failed to copy to clipboard after {MaxRetries} attempts: clipboard is still in use.", typeof(ClipboardHelper));
            return false;
        }
    }
}
