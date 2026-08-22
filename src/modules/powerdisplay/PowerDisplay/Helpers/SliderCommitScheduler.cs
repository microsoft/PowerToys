// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using PowerDisplay.Configuration;

namespace PowerDisplay.Helpers;

internal static class SliderCommitScheduler
{
    internal static void Schedule(ref DispatcherQueueTimer? timer, DispatcherQueue dispatcherQueue, Func<Task> commit)
    {
        if (timer == null)
        {
            timer = dispatcherQueue.CreateTimer();
            timer.IsRepeating = false;
            timer.Interval = TimeSpan.FromMilliseconds(AppConstants.UI.SliderCommitDebounceMs);
            timer.Tick += (_, _) => _ = commit();
        }

        timer.Stop();
        timer.Start();
    }
}
