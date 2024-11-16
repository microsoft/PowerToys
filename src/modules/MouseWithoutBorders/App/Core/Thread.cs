// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;

// <summary>
//     Logging.
// </summary>
// <history>
//     2008 created by Truong Do (ductdo).
//     2009-... modified by Truong Do (TruongDo).
//     2023- Included in PowerToys.
// </history>
namespace MouseWithoutBorders.Core;

internal sealed class Thread
{
    private static readonly Lock ThreadsLock = new();
    private static List<System.Threading.Thread> threads;

    private readonly System.Threading.Thread thread;

    internal Thread(ThreadStart callback, string name)
    {
        UpdateThreads(thread = new System.Threading.Thread(callback) { Name = name });
    }

    internal Thread(ParameterizedThreadStart callback, string name)
    {
        UpdateThreads(thread = new System.Threading.Thread(callback) { Name = name });
    }

    internal static void UpdateThreads(System.Threading.Thread thread)
    {
        lock (ThreadsLock)
        {
            bool found = false;
            List<System.Threading.Thread> toBeRemovedThreads = new();
            threads ??= new List<System.Threading.Thread>();

            foreach (System.Threading.Thread t in threads)
            {
                if (!t.IsAlive)
                {
                    toBeRemovedThreads.Add(t);
                }
                else if (t.ManagedThreadId == thread.ManagedThreadId)
                {
                    found = true;
                }
            }

            foreach (System.Threading.Thread t in toBeRemovedThreads)
            {
                _ = threads.Remove(t);
            }

            if (!found)
            {
                threads.Add(thread);
            }
        }
    }

    internal static string DumpThreadsStack()
    {
        string stack = "\r\nMANAGED THREADS: " + threads.Count.ToString(CultureInfo.InvariantCulture) + "\r\n";
        stack += Logger.GetStackTrace(new StackTrace());
        return stack;
    }

    internal void SetApartmentState(ApartmentState apartmentState)
    {
        thread.SetApartmentState(apartmentState);
    }

    internal void Start()
    {
        thread.Start();
    }

    internal void Start(object parameter)
    {
        thread.Start(parameter);
    }

    internal static void Sleep(int millisecondsTimeout)
    {
        System.Threading.Thread.Sleep(millisecondsTimeout);
    }

    internal static System.Threading.Thread CurrentThread => System.Threading.Thread.CurrentThread;

    internal ThreadPriority Priority
    {
        get => thread.Priority;
        set => thread.Priority = value;
    }

    internal System.Threading.ThreadState ThreadState => thread.ThreadState;
}
