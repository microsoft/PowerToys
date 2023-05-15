// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;

namespace Peek.Common.Extensions
{
    public static class DispatcherExtensions
    {
        /// <summary>
        /// Run work on UI thread safely.
        /// </summary>
        /// <returns>True if the work was run successfully, False otherwise.</returns>
        public static Task RunOnUiThread(this DispatcherQueue dispatcher, Func<Task> work)
        {
            var tcs = new TaskCompletionSource();
            dispatcher.TryEnqueue(async () =>
            {
                try
                {
                    await work();

                    tcs.SetResult();
                }
                catch (Exception e)
                {
                    tcs.SetException(e);
                }
            });

            return tcs.Task;
        }

        /// <summary>
        /// Run work on UI thread safely.
        /// </summary>
        /// <returns>True if the work was run successfully, False otherwise.</returns>
        public static Task RunOnUiThread(this DispatcherQueue dispatcher, Action work)
        {
            var tcs = new TaskCompletionSource();
            dispatcher.TryEnqueue(() =>
            {
                try
                {
                    work();

                    tcs.SetResult();
                }
                catch (Exception e)
                {
                    tcs.SetException(e);
                }
            });

            return tcs.Task;
        }
    }
}
