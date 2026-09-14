// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;

namespace ScreenTranslator.Helpers;

public static class NativeEventWaiter
{
    public static void WaitForEventLoop(
        string eventName,
        Action callback,
        CancellationToken cancellationToken)
    {
        Task.Run(
            () =>
            {
                EventWaitHandle? eventHandle = null;
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        if (eventHandle == null)
                        {
                            if (!EventWaitHandle.TryOpenExisting(eventName, out eventHandle))
                            {
                                Thread.Sleep(500);
                                continue;
                            }
                        }

                        int index = WaitHandle.WaitAny(new[] { eventHandle, cancellationToken.WaitHandle });
                        if (index == 0)
                        {
                            callback();
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Exception in NativeEventWaiter for {eventName}: {ex.Message}");
                        Thread.Sleep(1000);
                    }
                }

                eventHandle?.Dispose();
            },
            cancellationToken);
    }
}
