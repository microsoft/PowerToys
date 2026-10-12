// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.Run.Plugin.Calculator.UnitTests
{
    [TestClass]
    [DoNotParallelize]
    public class ClipboardHelperTests
    {
        [DataTestMethod]
        [DataRow("")]
        [DataRow("2")]
        [DataRow("1.5")]
        [DataRow("1,5")]
        [DataRow("中文 مرحبا 😀")]
        public void TextPersistsAfterCopyingThreadExits(string text)
        {
            WithPrivateClipboard(() =>
            {
                RunOnStaThread(() => ClipboardHelper.SetText(text));
                Assert.AreEqual(text, System.Windows.Clipboard.GetText());
            });
        }

        [TestMethod]
        public void ClipboardLockFailurePreservesPreviousContent()
        {
            WithPrivateClipboard(() =>
            {
                ClipboardHelper.SetText("previous");
                WithClipboardLocked(() => Assert.ThrowsException<Win32Exception>(() => ClipboardHelper.SetText("replacement")));
                Assert.AreEqual("previous", System.Windows.Clipboard.GetText());
            });
        }

        [TestMethod]
        public void OleFlushCanFailAfterTextHasAlreadyBeenPlacedOnClipboard()
        {
            WithPrivateClipboard(() =>
            {
                // This is the persistence phase used by WPF SetText. An observer can
                // acquire the clipboard between OleSetClipboard and OleFlushClipboard.
                System.Windows.Clipboard.SetDataObject("2", false);
                WithClipboardLocked(() => Assert.AreEqual(unchecked((int)0x800401D0), OleFlushClipboard()));
                Assert.AreEqual("2", System.Windows.Clipboard.GetText());

                RunOnStaThread(() => ClipboardHelper.SetText("3"));
                Assert.AreEqual("3", System.Windows.Clipboard.GetText());
            });
        }

        private static void WithClipboardLocked(Action action)
        {
            using var acquired = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            Exception failure = null;
            var holder = new Thread(() =>
            {
                if (!OpenClipboard(IntPtr.Zero))
                {
                    failure = new Win32Exception(Marshal.GetLastWin32Error());
                    acquired.Set();
                    return;
                }

                acquired.Set();
                try
                {
                    release.Wait();
                }
                finally
                {
                    CloseClipboard();
                }
            });
            holder.Start();
            acquired.Wait();
            try
            {
                if (failure != null)
                {
                    ExceptionDispatchInfo.Capture(failure).Throw();
                }

                action();
            }
            finally
            {
                release.Set();
                holder.Join();
            }
        }

        private static void RunOnStaThread(Action action)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null)
            {
                ExceptionDispatchInfo.Capture(failure).Throw();
            }
        }

        private static void WithPrivateClipboard(Action action)
        {
            Exception failure = null;
            var coordinator = new Thread(() =>
            {
                var originalStation = GetProcessWindowStation();
                var originalDesktop = GetThreadDesktop(GetCurrentThreadId());
                var station = IntPtr.Zero;
                var desktop = IntPtr.Zero;
                try
                {
                    // Clipboard state belongs to the window station. Isolate these
                    // integration tests from the user's interactive clipboard.
                    station = CreateWindowStation(null, 0, 0x037f, IntPtr.Zero);
                    if (station == IntPtr.Zero || !SetProcessWindowStation(station))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }

                    desktop = CreateDesktop("ClipboardTests", null, IntPtr.Zero, 0, 0x01ff, IntPtr.Zero);
                    if (desktop == IntPtr.Zero || !SetThreadDesktop(desktop))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }

                    RunOnStaThread(action);
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
                finally
                {
                    SetProcessWindowStation(originalStation);
                    SetThreadDesktop(originalDesktop);
                    if (desktop != IntPtr.Zero)
                    {
                        CloseDesktop(desktop);
                    }

                    if (station != IntPtr.Zero)
                    {
                        CloseWindowStation(station);
                    }
                }
            });
            coordinator.Start();
            coordinator.Join();
            if (failure != null)
            {
                ExceptionDispatchInfo.Capture(failure).Throw();
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateWindowStation(string name, uint flags, uint access, IntPtr attributes);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetProcessWindowStation(IntPtr station);

        [DllImport("user32.dll")]
        private static extern IntPtr GetProcessWindowStation();

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateDesktop(string name, string device, IntPtr devmode, uint flags, uint access, IntPtr attributes);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetThreadDesktop(IntPtr desktop);

        [DllImport("user32.dll")]
        private static extern IntPtr GetThreadDesktop(uint threadId);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseDesktop(IntPtr desktop);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseWindowStation(IntPtr station);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool OpenClipboard(IntPtr window);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseClipboard();

        [DllImport("ole32.dll")]
        private static extern int OleFlushClipboard();
    }
}
