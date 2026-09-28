// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using Windows.ApplicationModel.DataTransfer;

namespace AdvancedPaste.Cli;

internal sealed class SystemClipboardAdapter : IClipboardAdapter
{
    public DataPackageView Read()
        => RunOnSta(Clipboard.GetContent);

    public void Write(DataPackage content)
        => RunOnSta(() =>
        {
            Clipboard.SetContent(content);
            FlushClipboard(Clipboard.Flush);
            return true;
        });

    internal static void FlushClipboard(Action flush)
    {
        const int maxAttempts = 5;
        ExceptionDispatchInfo? failure = null;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                flush();
                return;
            }
            catch (Exception ex)
            {
                failure = ExceptionDispatchInfo.Capture(ex);
            }
        }

        failure!.Throw();
    }

    // Async entry points can resume on an MTA thread; clipboard calls always need STA.
    internal static T RunOnSta<T>(Func<T> operation)
    {
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            return operation();
        }

        T? result = default;
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = operation();
            }
            catch (Exception ex)
            {
                failure = ExceptionDispatchInfo.Capture(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        failure?.Throw();
        return result!;
    }
}
