// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Dock;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Win32;
using Windows.Win32.UI.Accessibility;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
[DoNotParallelize]
public class WindowsEventsListenerTests
{
    [TestMethod]
    public void EventSubscriptionsDoNotControlHookLifetime()
    {
        using var hook = new WindowsEventsListener(static _ => (HWINEVENTHOOK)(nint)1, static _ => true);
        List<bool> firstSubscriber = [];
        List<bool> secondSubscriber = [];
        object? eventSender = null;
        EventHandler<ForegroundWindowChangedEventArgs> firstCallback = (sender, args) =>
        {
            eventSender = sender;
            firstSubscriber.Add(args.IsDesktopWindow);
        };
        EventHandler<ForegroundWindowChangedEventArgs> secondCallback = (_, args) => secondSubscriber.Add(args.IsDesktopWindow);
        bool[] oneNotification = [false];
        bool[] twoNotifications = [false, false];

        hook.ForegroundWindowChanged += firstCallback;
        hook.ForegroundWindowChanged += secondCallback;
        Assert.IsFalse(hook.IsHooked);
        Assert.IsTrue(hook.Start());

        // Invoke the callback without changing the user's foreground window.
        hook.WinEventCallback(default, PInvoke.EVENT_SYSTEM_FOREGROUND, default, 0, 0, 0, 0);
        Assert.AreSame(hook, eventSender);
        CollectionAssert.AreEqual(oneNotification, firstSubscriber);
        CollectionAssert.AreEqual(oneNotification, secondSubscriber);

        hook.ForegroundWindowChanged -= firstCallback;
        Assert.IsTrue(hook.IsHooked);
        hook.WinEventCallback(default, PInvoke.EVENT_SYSTEM_FOREGROUND, default, 0, 0, 0, 0);
        CollectionAssert.AreEqual(oneNotification, firstSubscriber);
        CollectionAssert.AreEqual(twoNotifications, secondSubscriber);

        hook.ForegroundWindowChanged -= secondCallback;
        Assert.IsTrue(hook.IsHooked);
        hook.Stop();
        Assert.IsFalse(hook.IsHooked);

        hook.ForegroundWindowChanged += firstCallback;
        hook.WinEventCallback(default, PInvoke.EVENT_SYSTEM_FOREGROUND, default, 0, 0, 0, 0);
        CollectionAssert.AreEqual(oneNotification, firstSubscriber);

        Assert.IsTrue(hook.Start());
        hook.WinEventCallback(default, PInvoke.EVENT_SYSTEM_FOREGROUND, default, 0, 0, 0, 0);
        CollectionAssert.AreEqual(twoNotifications, firstSubscriber);
        CollectionAssert.AreEqual(twoNotifications, secondSubscriber);
    }

    [TestMethod]
    public void StartStopAndDisposalMustUseOwningThread()
    {
        using var hook = new WindowsEventsListener(static _ => (HWINEVENTHOOK)(nint)1, static _ => true);

        AssertWrongThreadRejected(() => hook.Start());
        Assert.IsFalse(hook.IsHooked);

        Assert.IsTrue(hook.Start());
        AssertWrongThreadRejected(hook.Stop);
        AssertWrongThreadRejected(hook.Dispose);
        Assert.IsTrue(hook.IsHooked);
        Assert.IsTrue(hook.Start());

        hook.Stop();
        Assert.IsFalse(hook.IsHooked);
    }

    [TestMethod]
    public void FailedStartCanRetryWithoutLosingSubscribers()
    {
        var registrations = 0;
        var notifications = 0;
        using var hook = new WindowsEventsListener(
            _ => ++registrations == 1 ? default : (HWINEVENTHOOK)(nint)1,
            static _ => true);
        hook.ForegroundWindowChanged += (_, _) => notifications++;

        Assert.IsFalse(hook.Start());
        Assert.IsFalse(hook.IsHooked);

        Assert.IsTrue(hook.Start());
        Assert.IsTrue(hook.Start());
        Assert.IsTrue(hook.IsHooked);
        Assert.AreEqual(2, registrations);

        hook.WinEventCallback(default, PInvoke.EVENT_SYSTEM_FOREGROUND, default, 0, 0, 0, 0);
        Assert.AreEqual(1, notifications);

        hook.Stop();
        Assert.IsTrue(hook.Start());
        hook.WinEventCallback(default, PInvoke.EVENT_SYSTEM_FOREGROUND, default, 0, 0, 0, 0);
        Assert.AreEqual(2, notifications);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void FailedUnhookKeepsCallbackAliveUntilSuccessfulRetry(bool dispose)
    {
        WeakReference? nativeCallback = null;
        var canUnhook = false;
        var notifications = 0;
        using var hook = new WindowsEventsListener(
            hookProc =>
            {
                nativeCallback = new WeakReference(hookProc);
                return (HWINEVENTHOOK)(nint)1;
            },
            _ => canUnhook);
        hook.ForegroundWindowChanged += (_, _) => notifications++;
        Action cleanup = dispose ? hook.Dispose : hook.Stop;

        try
        {
            Assert.IsTrue(hook.Start());
            cleanup();
            Assert.IsTrue(hook.IsHooked);
            if (dispose)
            {
                Assert.ThrowsExactly<ObjectDisposedException>(() => hook.Start());
            }

            GC.Collect();
            Assert.IsTrue(nativeCallback!.IsAlive);

            hook.WinEventCallback(default, PInvoke.EVENT_SYSTEM_FOREGROUND, default, 0, 0, 0, 0);
            Assert.AreEqual(0, notifications);

            canUnhook = true;
            cleanup();
            Assert.IsFalse(hook.IsHooked);

            GC.Collect();
            Assert.IsFalse(nativeCallback.IsAlive);
        }
        finally
        {
            canUnhook = true;
            hook.Stop();
        }
    }

    [TestMethod]
    public void StartAfterFailedStopReusesNativeHook()
    {
        var registrations = 0;
        var notifications = 0;
        var canUnhook = false;
        using var hook = new WindowsEventsListener(
            _ =>
            {
                registrations++;
                return (HWINEVENTHOOK)(nint)1;
            },
            _ => canUnhook);
        hook.ForegroundWindowChanged += (_, _) => notifications++;

        try
        {
            Assert.IsTrue(hook.Start());
            hook.Stop();
            Assert.IsTrue(hook.Start());
            Assert.AreEqual(1, registrations);

            hook.WinEventCallback(default, PInvoke.EVENT_SYSTEM_FOREGROUND, default, 0, 0, 0, 0);
            Assert.AreEqual(1, notifications);
        }
        finally
        {
            canUnhook = true;
            hook.Stop();
        }
    }

    [TestMethod]
    public void SubscriberExceptionDoesNotEscapeNativeCallback()
    {
        using var hook = new WindowsEventsListener(static _ => (HWINEVENTHOOK)(nint)1, static _ => true);
        hook.ForegroundWindowChanged += static (_, _) => throw new InvalidOperationException("Subscriber failed.");

        Assert.IsTrue(hook.Start());
        hook.WinEventCallback(default, PInvoke.EVENT_SYSTEM_FOREGROUND, default, 0, 0, 0, 0);
        Assert.IsTrue(hook.IsHooked);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void DisposalClearsSubscribersAndCannotRestart(bool start, bool stopFirst)
    {
        var unhooks = 0;
        var notifications = 0;
        using var hook = new WindowsEventsListener(
            static _ => (HWINEVENTHOOK)(nint)1,
            _ =>
            {
                unhooks++;
                return true;
            });
        hook.ForegroundWindowChanged += (_, _) => notifications++;

        if (start)
        {
            Assert.IsTrue(hook.Start());
        }

        if (stopFirst)
        {
            hook.Stop();
        }

        hook.Dispose();
        Assert.IsFalse(hook.IsHooked);
        Assert.ThrowsExactly<ObjectDisposedException>(() => hook.Start());

        hook.WinEventCallback(default, PInvoke.EVENT_SYSTEM_FOREGROUND, default, 0, 0, 0, 0);
        Assert.AreEqual(0, notifications);

        hook.Stop();
        hook.Dispose();
        Assert.AreEqual(start ? 1 : 0, unhooks);
    }

    private static void AssertWrongThreadRejected(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        })
        {
            IsBackground = true,
        };

        thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(5)), "The test thread did not finish.");
        Assert.AreEqual(typeof(InvalidOperationException), error?.GetType());
    }
}
