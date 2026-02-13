// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using ManagedCommon;

namespace Awake.Core.Threading
{
    internal sealed class SingleThreadSynchronizationContext : SynchronizationContext
    {
        private readonly Queue<(SendOrPostCallback Callback, object? State)?> queue = new();

        public override void Post(SendOrPostCallback d, object? state)
        {
            ArgumentNullException.ThrowIfNull(d);

            lock (queue)
            {
                queue.Enqueue((d, state));
                Monitor.Pulse(queue);
            }
        }

        public void BeginMessageLoop()
        {
            while (true)
            {
                (SendOrPostCallback Callback, object? State)? work;
                lock (queue)
                {
                    while (queue.Count == 0)
                    {
                        Monitor.Wait(queue);
                    }

                    work = queue.Dequeue();
                }

                if (work == null)
                {
                    break;
                }

                try
                {
                    work.Value.Callback(work.Value.State);
                }
                catch (Exception e)
                {
                    Logger.LogError("Error during execution of the STS context message loop: " + e.Message);
                }
            }
        }

        public void EndMessageLoop()
        {
            lock (queue)
            {
                // Signal the end of the message loop
                queue.Enqueue(null);
                Monitor.Pulse(queue);
            }
        }
    }
}
