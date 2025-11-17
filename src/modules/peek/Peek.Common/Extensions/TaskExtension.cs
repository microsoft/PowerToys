// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;

namespace Peek.Common.Extensions
{
    public static class TaskExtension
    {
        public static Task<bool> RunSafe(Func<Task> work)
        {
            var tcs = new TaskCompletionSource<bool>();
            Task.Run(async () =>
            {
                try
                {
                    await work();

                    tcs.SetResult(true);
                }
                catch (Exception)
                {
                    tcs.SetResult(false);
                }
            });

            return tcs.Task;
        }
    }
}
