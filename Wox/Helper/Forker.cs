using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Wox.Helper
{
    /// <summary>
    /// Provides a caller-friendly wrapper around parallel actions.
    /// http://stackoverflow.com/a/540380
    /// </summary>
    public sealed class Forker
    {
        int running;
        private readonly object joinLock = new object(), eventLock = new object();

        /// <summary>Raised when all operations have completed.</summary>
        public event EventHandler AllComplete
        {
            add { lock (eventLock) { allComplete += value; } }
            remove { lock (eventLock) { allComplete -= value; } }
        }
        private EventHandler allComplete;
        /// <summary>Raised when each operation completes.</summary>
        public event EventHandler<ParallelEventArgs> ItemComplete
        {
            add { lock (eventLock) { itemComplete += value; } }
            remove { lock (eventLock) { itemComplete -= value; } }
        }
        private EventHandler<ParallelEventArgs> itemComplete;

        private void OnItemComplete(object state, Exception exception)
        {
            EventHandler<ParallelEventArgs> itemHandler = itemComplete; // don't need to lock
            if (itemHandler != null) itemHandler(this, new ParallelEventArgs(state, exception));
            if (Interlocked.Decrement(ref running) == 0)
            {
                EventHandler allHandler = allComplete; // don't need to lock
                if (allHandler != null) allHandler(this, EventArgs.Empty);
                lock (joinLock)
                {
                    Monitor.PulseAll(joinLock);
                }
            }
        }

        /// <summary>Adds a callback to invoke when each operation completes.</summary>
        /// <returns>Current instance (for fluent API).</returns>
        public Forker OnItemComplete(EventHandler<ParallelEventArgs> handler)
        {
            if (handler == null) throw new ArgumentNullException("handler");
            ItemComplete += handler;
            return this;
        }

        /// <summary>Adds a callback to invoke when all operations are complete.</summary>
        /// <returns>Current instance (for fluent API).</returns>
        public Forker OnAllComplete(EventHandler handler)
        {
            if (handler == null) throw new ArgumentNullException("handler");
            AllComplete += handler;
            return this;
        }

        /// <summary>Waits for all operations to complete.</summary>
        public void Join()
        {
            Join(-1);
        }

        /// <summary>Waits (with timeout) for all operations to complete.</summary>
        /// <returns>Whether all operations had completed before the timeout.</returns>
        public bool Join(int millisecondsTimeout)
        {
            lock (joinLock)
            {
                if (CountRunning() == 0) return true;
                Thread.SpinWait(1); // try our luck...
                return (CountRunning() == 0) ||
                    Monitor.Wait(joinLock, millisecondsTimeout);
            }
        }

        /// <summary>Indicates the number of incomplete operations.</summary>
        /// <returns>The number of incomplete operations.</returns>
        public int CountRunning()
        {
            return Interlocked.CompareExchange(ref running, 0, 0);
        }

        /// <summary>Enqueues an operation.</summary>
        /// <param name="action">The operation to perform.</param>
        /// <returns>The current instance (for fluent API).</returns>
        public Forker Fork(ThreadStart action) { return Fork(action, null); }

        /// <summary>Enqueues an operation.</summary>
        /// <param name="action">The operation to perform.</param>
        /// <param name="state">An opaque object, allowing the caller to identify operations.</param>
        /// <returns>The current instance (for fluent API).</returns>
        public Forker Fork(ThreadStart action, object state)
        {
            if (action == null) throw new ArgumentNullException("action");
            Interlocked.Increment(ref running);
            ThreadPool.QueueUserWorkItem(delegate
            {
                Exception exception = null;
                try { action(); }
                catch (Exception ex) { exception = ex; }
                OnItemComplete(state, exception);
            });
            return this;
        }


        /// <summary>Event arguments representing the completion of a parallel action.</summary>
        public class ParallelEventArgs : EventArgs
        {
            private readonly object state;
            private readonly Exception exception;
            internal ParallelEventArgs(object state, Exception exception)
            {
                this.state = state;
                this.exception = exception;
            }

            /// <summary>The opaque state object that identifies the action (null otherwise).</summary>
            public object State { get { return state; } }

            /// <summary>The exception thrown by the parallel action, or null if it completed without exception.</summary>
            public Exception Exception { get { return exception; } }
        }
    }
}
