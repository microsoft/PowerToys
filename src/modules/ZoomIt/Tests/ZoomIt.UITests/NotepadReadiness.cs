// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.ZoomIt.UITests;

internal static class NotepadReadiness
{
    internal static WaitHelper.StableWaitResult<T> WaitForStableWindow<T>(
        Func<T?> observeReadyWindow,
        Func<T, long> windowHandle,
        int timeoutMS = 30_000,
        int pollIntervalMS = 200)
        where T : class
    {
        long previousHandle = 0;
        return WaitHelper.WaitForStable(
            observeReadyWindow,
            candidate =>
            {
                long currentHandle = candidate is null ? 0 : windowHandle(candidate);
                bool stable = currentHandle != 0 && currentHandle == previousHandle;
                previousHandle = currentHandle;
                return stable;
            },
            timeoutMS,
            pollIntervalMS: pollIntervalMS,
            shouldRetryException: exception =>
            {
                if (!IsTransientException(exception))
                {
                    return false;
                }

                previousHandle = 0;
                return true;
            });
    }

    internal static bool IsTransientException(Exception exception) =>
        ShellMenu.IsTransientElementException(exception) ||
        (exception is AssertFailedException &&
            exception.Message.Contains("internal_error", StringComparison.OrdinalIgnoreCase) &&
            exception.Message.Contains("Window HWND ", StringComparison.OrdinalIgnoreCase) &&
            exception.Message.Contains("not found or not accessible", StringComparison.OrdinalIgnoreCase));
}
