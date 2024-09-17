// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;

namespace Awake.Core.Models
{
    internal sealed class SingleThreadSynchronizationContext : SynchronizationContext
    {
        private readonly Queue<Tuple<SendOrPostCallback, object>> queue =
            new();

#pragma warning disable CS8765 // Nullability of type of parameter doesn't match overridden member (possibly because of nullability attributes).
        public override void Post(SendOrPostCallback d, object state)
#pragma warning restore CS8765 // Nullability of type of parameter doesn't match overridden member (possibly because of nullability attributes).
        {
            lock (queue)
            {
                queue.Enqueue(Tuple.Create(d, state));
                Monitor.Pulse(queue);
            }
        }

        public void BeginMessageLoop()
        {
            while (true)
            {
                Tuple<SendOrPostCallback, object> work;
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

                work.Item1(work.Item2);
            }
        }

        public void EndMessageLoop()
        {
            lock (queue)
            {
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
                queue.Enqueue(null);  // Signal the end of the message loop
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
                Monitor.Pulse(queue);
            }
        }
    }
}
