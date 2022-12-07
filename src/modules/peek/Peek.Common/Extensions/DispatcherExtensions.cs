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
        public static Task<TaskStatus> TryEnqueueSafe(this DispatcherQueue dispatcher, Func<Task> work)
        {
            var tcs = new TaskCompletionSource<TaskStatus>();
            dispatcher.TryEnqueue(async () =>
            {
                try
                {
                    await work();

                    tcs.SetResult(TaskStatus.RanToCompletion);
                }
                catch (Exception)
                {
                    tcs.SetResult(TaskStatus.Faulted);
                }
            });

            return tcs.Task;
        }
    }
}
