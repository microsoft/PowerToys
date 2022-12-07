// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.Common.Extensions
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.UI.Dispatching;

    public static class DispatcherExtensions
    {
        public static Task TryEnqueueSafe(this DispatcherQueue dispatcher, Func<TaskCompletionSource, Task> work)
        {
            var tcs = new TaskCompletionSource();
            dispatcher.TryEnqueue(async () =>
            {
                try
                {
                    await work(tcs);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });

            return tcs.Task;
        }
    }
}
