// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using MouseWithoutBorders.Core;

namespace MouseWithoutBorders.UnitTests.Core;

[TestClass]
public sealed class InputHookDispatcherTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [TestMethod]
    public void PostedWorkRunsInOrderOffTheCallingThread()
    {
        using var dispatcher = new InputHookDispatcher("test dispatcher");
        var order = new ConcurrentQueue<int>();
        var threadIds = new ConcurrentBag<int>();
        using var done = new ManualResetEventSlim();

        for (int i = 0; i < 200; i++)
        {
            int value = i;
            Assert.IsTrue(dispatcher.Post(() =>
            {
                threadIds.Add(Environment.CurrentManagedThreadId);
                order.Enqueue(value);
            }));
        }

        Assert.IsTrue(dispatcher.Post(done.Set));
        Assert.IsTrue(done.Wait(Timeout));

        CollectionAssert.AreEqual(Enumerable.Range(0, 200).ToArray(), order.ToArray());
        Assert.AreEqual(1, threadIds.Distinct().Count());
        Assert.AreNotEqual(Environment.CurrentManagedThreadId, threadIds.First());
    }

    [TestMethod]
    public void RunOffHookQueuesOnlyInsideHookCallback()
    {
        int callingThread = Environment.CurrentManagedThreadId;

        int inlineThread = 0;
        InputHookDispatcher.RunOffHook(() => inlineThread = Environment.CurrentManagedThreadId);
        Assert.AreEqual(callingThread, inlineThread);

        int queuedThread = 0;
        using var done = new ManualResetEventSlim();
        using (InputHookDispatcher.EnterHookCallback())
        {
            Assert.IsTrue(InputHookDispatcher.IsInHookCallback);
            InputHookDispatcher.RunOffHook(() =>
            {
                queuedThread = Environment.CurrentManagedThreadId;
                done.Set();
            });
        }

        Assert.IsFalse(InputHookDispatcher.IsInHookCallback);
        Assert.IsTrue(done.Wait(Timeout));
        Assert.AreNotEqual(callingThread, queuedThread);
    }

    [TestMethod]
    public void HookCallbackScopeRestoresPreviousStateWhenNested()
    {
        using (InputHookDispatcher.EnterHookCallback())
        {
            using (InputHookDispatcher.EnterHookCallback())
            {
                Assert.IsTrue(InputHookDispatcher.IsInHookCallback);
            }

            Assert.IsTrue(InputHookDispatcher.IsInHookCallback);
        }

        Assert.IsFalse(InputHookDispatcher.IsInHookCallback);
    }

    [TestMethod]
    public void CoalescibleWorkKeepsOnlyTheLatestWithoutReordering()
    {
        using var dispatcher = new InputHookDispatcher("test dispatcher");
        using var gate = new ManualResetEventSlim();
        using var started = new ManualResetEventSlim();
        using var done = new ManualResetEventSlim();
        var ran = new ConcurrentQueue<string>();

        // Hold the worker so everything below stays queued.
        Assert.IsTrue(dispatcher.Post(() =>
        {
            started.Set();
            gate.Wait(Timeout);
        }));
        Assert.IsTrue(started.Wait(Timeout));

        Assert.IsTrue(dispatcher.Post(() => ran.Enqueue("move1"), coalesce: true));
        Assert.IsTrue(dispatcher.Post(() => ran.Enqueue("move2"), coalesce: true));
        Assert.IsTrue(dispatcher.Post(() => ran.Enqueue("key"), coalesce: false));
        Assert.IsTrue(dispatcher.Post(() => ran.Enqueue("move3"), coalesce: true));
        Assert.IsTrue(dispatcher.Post(() => ran.Enqueue("move4"), coalesce: true));
        Assert.IsTrue(dispatcher.Post(() => ran.Enqueue("button"), coalesce: false));
        Assert.AreEqual(2, dispatcher.CoalescedCount);

        gate.Set();
        Assert.IsTrue(dispatcher.Post(done.Set));
        Assert.IsTrue(done.Wait(Timeout));

        string[] expected = ["move2", "key", "move4", "button"];
        CollectionAssert.AreEqual(expected, ran.ToArray());
    }

    [TestMethod]
    public void CoalescibleWorkIsNotMergedIntoWorkThatAlreadyStarted()
    {
        using var dispatcher = new InputHookDispatcher("test dispatcher");
        using var gate = new ManualResetEventSlim();
        using var started = new ManualResetEventSlim();
        using var done = new ManualResetEventSlim();
        var ran = new ConcurrentQueue<string>();

        Assert.IsTrue(dispatcher.Post(
            () =>
            {
                ran.Enqueue("move1");
                started.Set();
                gate.Wait(Timeout);
            },
            coalesce: true));
        Assert.IsTrue(started.Wait(Timeout));

        Assert.IsTrue(dispatcher.Post(() => ran.Enqueue("move2"), coalesce: true));
        Assert.AreEqual(0, dispatcher.CoalescedCount);

        gate.Set();
        Assert.IsTrue(dispatcher.Post(done.Set));
        Assert.IsTrue(done.Wait(Timeout));

        string[] expected = ["move1", "move2"];
        CollectionAssert.AreEqual(expected, ran.ToArray());
    }

    [TestMethod]
    public void FailingWorkDoesNotStopTheDispatcher()
    {
        using var dispatcher = new InputHookDispatcher("test dispatcher");
        using var done = new ManualResetEventSlim();

        Assert.IsTrue(dispatcher.Post(() => throw new InvalidOperationException("test")));
        Assert.IsTrue(dispatcher.Post(done.Set));

        Assert.IsTrue(done.Wait(Timeout));
    }

    [TestMethod]
    public void PostAfterDisposeIsRejected()
    {
        var dispatcher = new InputHookDispatcher("test dispatcher");
        dispatcher.Dispose();

        Assert.IsFalse(dispatcher.Post(() => { }));
        Assert.IsFalse(dispatcher.Post(() => { }, coalesce: true));
    }

    [TestMethod]
    public void DataCloneIsIndependentOfTheReusedPackage()
    {
        var package = new DATA
        {
            Type = PackageType.Mouse,
            Des = (ID)7,
        };
        package.Md.X = 10;
        package.Md.Y = 20;
        package.Md.dwFlags = WM.WM_LBUTTONUP;

        DATA snapshot = package.Clone();
        package.Md.X = 99;
        package.Md.dwFlags = WM.WM_MOUSEMOVE;
        package.Des = (ID)8;

        Assert.AreNotSame(package, snapshot);
        Assert.AreEqual(PackageType.Mouse, snapshot.Type);
        Assert.AreEqual((ID)7, snapshot.Des);
        Assert.AreEqual(10, snapshot.Md.X);
        Assert.AreEqual(20, snapshot.Md.Y);
        Assert.AreEqual(WM.WM_LBUTTONUP, snapshot.Md.dwFlags);
    }
}
