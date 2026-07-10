// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Threading;

using ManagedCommon;
using Windows.ApplicationModel.DataTransfer;

namespace ColorPicker.Helpers
{
    public static class ClipboardHelper
    {
        public static void CopyToClipboard(string colorRepresentationToCopy)
        {
            if (string.IsNullOrEmpty(colorRepresentationToCopy))
            {
                return;
            }

            if (!TrySetClipboard(
                () =>
                {
                    var data = new DataPackage();
                    data.SetText(colorRepresentationToCopy);
                    Clipboard.SetContent(data);
                    Clipboard.Flush(); // persist after this process exits (Color Picker may close immediately)
                },
                maxAttempts: 10,
                delayMs: 10,
                out Exception lastException))
            {
                Logger.LogError("Failed to set text into clipboard", lastException);
            }
        }

        /// <summary>
        /// Invokes <paramref name="attempt"/> up to <paramref name="maxAttempts"/> times,
        /// retrying on <see cref="COMException"/> or <see cref="UnauthorizedAccessException"/>,
        /// sleeping <paramref name="delayMs"/> ms between attempts (not after the final one).
        /// Any other exception propagates immediately.
        /// </summary>
        /// <returns><see langword="true"/> on success; <see langword="false"/> when all attempts fail.</returns>
        internal static bool TrySetClipboard(Action attempt, int maxAttempts, int delayMs, out Exception lastException)
        {
            lastException = null;
            for (int i = 0; i < maxAttempts; i++)
            {
                if (i > 0)
                {
                    Thread.Sleep(delayMs);
                }

                try
                {
                    attempt();
                    lastException = null;
                    return true;
                }
                catch (COMException ex)
                {
                    lastException = ex;
                }
                catch (UnauthorizedAccessException ex)
                {
                    lastException = ex;
                }
            }

            return false;
        }
    }
}
