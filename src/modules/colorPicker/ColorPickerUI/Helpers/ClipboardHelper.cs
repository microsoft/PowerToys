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
    internal static class ClipboardHelper
    {
        private const int MaxAttempts = 10;
        private const int RetryDelayMilliseconds = 10;

        internal static void CopyToClipboard(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            Exception lastException = null;

            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                try
                {
                    // Both Color Picker copy paths run on the WinUI STA thread.
                    var package = new DataPackage();
                    package.SetText(text);
                    Clipboard.SetContent(package);
                    Clipboard.Flush(); // Keep the text available if Color Picker exits.
                    return;
                }
                catch (COMException ex)
                {
                    lastException = ex;
                }
                catch (UnauthorizedAccessException ex)
                {
                    lastException = ex;
                }

                if (attempt < MaxAttempts)
                {
                    Thread.Sleep(RetryDelayMilliseconds);
                }
            }

            Logger.LogError("Failed to set text into clipboard", lastException);
        }
    }
}
