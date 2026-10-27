// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Microsoft.GrabAndMove.UITests;

internal sealed class InputTrace : IDisposable
{
    private readonly ConcurrentQueue<Sample> samples = new();
    private readonly ManualResetEventSlim ready = new();
    private readonly Thread worker;
    private readonly HookCallback keyboardCallback;
    private readonly HookCallback mouseCallback;
    private uint threadId;
    private Exception? failure;

    internal InputTrace()
    {
        keyboardCallback = (code, message, data) =>
        {
            if (code >= 0)
            {
                samples.Enqueue(new Sample("keyboard", message.ToInt32(), Marshal.ReadInt32(data), Marshal.ReadInt32(data, 8)));
            }

            return CallNextHookEx(IntPtr.Zero, code, message, data);
        };
        mouseCallback = (code, message, data) =>
        {
            if (code >= 0)
            {
                samples.Enqueue(new Sample("mouse", message.ToInt32(), 0, Marshal.ReadInt32(data, 12)));
            }

            return CallNextHookEx(IntPtr.Zero, code, message, data);
        };
        worker = new Thread(Run) { IsBackground = true, Name = "UMDF prototype input trace" };
        worker.Start();
        if (!ready.Wait(TimeSpan.FromSeconds(10)))
        {
            throw new TimeoutException("Input trace did not start.");
        }

        if (failure is not null)
        {
            throw new InvalidOperationException("Input trace failed to start.", failure);
        }
    }

    private delegate IntPtr HookCallback(int code, IntPtr message, IntPtr data);

    internal Sample[] Samples => samples.ToArray();

    internal void Clear() => samples.Clear();

    public void Dispose()
    {
        _ = PostThreadMessageW(threadId, 0x12, IntPtr.Zero, IntPtr.Zero);
        if (!worker.Join(TimeSpan.FromSeconds(5)))
        {
            throw new TimeoutException("Input trace did not stop.");
        }

        ready.Dispose();
        if (failure is not null)
        {
            throw new InvalidOperationException("Input trace failed.", failure);
        }
    }

    private void Run()
    {
        IntPtr keyboard = IntPtr.Zero;
        IntPtr mouse = IntPtr.Zero;
        try
        {
            threadId = GetCurrentThreadId();
            var module = GetModuleHandleW(null);
            keyboard = SetWindowsHookExW(13, keyboardCallback, module, 0);
            mouse = SetWindowsHookExW(14, mouseCallback, module, 0);
            if (keyboard == IntPtr.Zero || mouse == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            ready.Set();
            int result;
            while ((result = GetMessageW(out _, IntPtr.Zero, 0, 0)) > 0)
            {
            }

            if (result == -1)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }
        catch (Exception error)
        {
            failure = error;
            ready.Set();
        }
        finally
        {
            if (keyboard != IntPtr.Zero)
            {
                UnhookWindowsHookEx(keyboard);
            }

            if (mouse != IntPtr.Zero)
            {
                UnhookWindowsHookEx(mouse);
            }
        }
    }

    internal readonly record struct Sample(string Kind, int Message, int Key, int Flags);

    [StructLayout(LayoutKind.Sequential, Size = 48)]
    private struct Message
    {
        internal IntPtr Window;
        internal uint Id;
        internal IntPtr WParam;
        internal IntPtr LParam;
        internal uint Time;
        internal int X;
        internal int Y;
        internal uint Private;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern IntPtr SetWindowsHookExW(int type, HookCallback callback, IntPtr module, uint thread);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern int GetMessageW(out Message message, IntPtr window, uint minimum, uint maximum);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostThreadMessageW(uint thread, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern IntPtr GetModuleHandleW(string? name);
}
