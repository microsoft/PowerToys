// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ManagedCommon.UnitTests;

[TestClass]
public sealed class WindowHelpersTests
{
    private const uint WmNull = 0x0000;
    private const uint SmtoNormal = 0x0000;
    private const uint PmNoRemove = 0x0000;

    // IntPtr.Zero is never a window, so the real SetForegroundWindow/AttachThreadInput calls that
    // BringToForeground makes after the faked input fail without touching the desktop.
    private static readonly IntPtr NoWindow = IntPtr.Zero;

    private static readonly IntPtr HwndMessage = new(-3);

    private static readonly TimeSpan InputTimeout = TimeSpan.FromMilliseconds(200);

    // Generous so slow CI agents can't flake; a regression blocks until the faked SendInput is released.
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);

    [TestMethod]
    public void BringToForegroundReturnsWhenSendInputNeverReturns()
    {
        // Reproduces an AppHang in win32u!NtUserSendInput: the Settings UI thread called BringToForeground from
        // App.OnLaunchedFromRunner and never got out of SendInput, so Settings hung on startup.
        using var release = new ManualResetEventSlim(false);
        var injector = new ForegroundInputInjector(() => release.Wait());

        try
        {
            Assert.IsTrue(
                Task.Run(() => WindowHelpers.BringToForeground(NoWindow, injector, InputTimeout)).Wait(Deadline),
                "BringToForeground blocked its caller on a SendInput call that never returns.");
        }
        finally
        {
            release.Set();
            WaitUntilIdle(injector);
        }
    }

    [TestMethod]
    public void BringToForegroundDoesNotPileUpBehindAStuckSendInput()
    {
        using var release = new ManualResetEventSlim(false);
        int sends = 0;
        var injector = new ForegroundInputInjector(() =>
        {
            Interlocked.Increment(ref sends);
            release.Wait();
        });

        try
        {
            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(
                    Task.Run(() => WindowHelpers.BringToForeground(NoWindow, injector, InputTimeout)).Wait(Deadline),
                    $"Call {i} blocked on a stuck SendInput call.");
            }

            Assert.AreEqual(1, Volatile.Read(ref sends), "Only one SendInput call should be outstanding at a time.");
        }
        finally
        {
            release.Set();
            WaitUntilIdle(injector);
        }

        // Once the stuck call returns, the next BringToForeground sends input again.
        WindowHelpers.BringToForeground(NoWindow, injector, Deadline);
        Assert.AreEqual(2, Volatile.Read(ref sends));
    }

    [TestMethod]
    public void BringToForegroundSendsInputOffTheCallingThreadAndWaitsForIt()
    {
        int callerThreadId = Environment.CurrentManagedThreadId;
        int sendThreadId = callerThreadId;
        bool sent = false;
        var injector = new ForegroundInputInjector(() =>
        {
            sendThreadId = Environment.CurrentManagedThreadId;
            Volatile.Write(ref sent, true);
        });

        WindowHelpers.BringToForeground(NoWindow, injector, Deadline);

        // SetForegroundWindow only gets past the foreground lock if the input was sent before it.
        Assert.IsTrue(Volatile.Read(ref sent), "BringToForeground returned before the input was sent.");
        Assert.AreNotEqual(callerThreadId, sendThreadId, "SendInput must not run on the caller's (UI) thread.");
    }

    [TestMethod]
    public void BringToForegroundDoesNotDispatchSentMessagesWhileWaitingForInput()
    {
        // The inline SendInput call never dispatched messages, but Thread.Join/WaitHandle.WaitOne would on an STA
        // thread, re-entering UI code (e.g. Settings in the middle of OnLaunched) while the input is outstanding.
        using var sending = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        using var windowCreated = new ManualResetEventSlim(false);
        var injector = new ForegroundInputInjector(() =>
        {
            sending.Set();
            release.Wait();
        });

        IntPtr window = IntPtr.Zero;
        bool returned = false;
        bool messageHandled = false;
        var uiThread = new Thread(() =>
        {
            window = CreateWindowEx(0, "STATIC", string.Empty, 0, 0, 0, 0, 0, HwndMessage, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            windowCreated.Set();
            if (window == IntPtr.Zero)
            {
                return;
            }

            WindowHelpers.BringToForeground(NoWindow, injector, TimeSpan.FromSeconds(1));
            Volatile.Write(ref returned, true);

            // Pump until the message sent during BringToForeground has been handled.
            while (!Volatile.Read(ref messageHandled))
            {
                PeekMessage(out _, IntPtr.Zero, 0, 0, PmNoRemove);
                Thread.Sleep(1);
            }

            DestroyWindow(window);
        });
        uiThread.SetApartmentState(ApartmentState.STA);
        uiThread.Start();

        try
        {
            Assert.IsTrue(windowCreated.Wait(Deadline) && window != IntPtr.Zero, "Couldn't create the test window.");
            Assert.IsTrue(sending.Wait(Deadline), "BringToForeground didn't send input.");

            IntPtr handled = SendMessageTimeout(window, WmNull, IntPtr.Zero, IntPtr.Zero, SmtoNormal, (uint)Deadline.TotalMilliseconds, out _);
            bool returnedFirst = Volatile.Read(ref returned);
            Volatile.Write(ref messageHandled, true);

            Assert.AreNotEqual(IntPtr.Zero, handled, "The sent message was never handled.");
            Assert.IsTrue(returnedFirst, "BringToForeground dispatched a sent message while waiting for SendInput.");
        }
        finally
        {
            Volatile.Write(ref messageHandled, true);
            release.Set();
            WaitUntilIdle(injector);
            uiThread.Join(Deadline);
        }
    }

    private static void WaitUntilIdle(ForegroundInputInjector injector)
    {
        Assert.IsTrue(SpinWait.SpinUntil(() => !injector.IsSendInProgress, Deadline), "The released SendInput call did not finish.");
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowEx(int exStyle, string className, string windowName, int style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool PeekMessage(out MSG msg, IntPtr hwnd, uint filterMin, uint filterMax, uint removeMsg);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr Hwnd;
        public uint Message;
        public IntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public int PtX;
        public int PtY;
    }
}
