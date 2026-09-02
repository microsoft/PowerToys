// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Microsoft.PowerToys.KeyboardManager.UITests;

internal sealed class KeyboardEventRecorder : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const uint WmKeyDown = 0x0100;
    private const uint WmKeyUp = 0x0101;
    private const uint WmSysKeyDown = 0x0104;
    private const uint WmSysKeyUp = 0x0105;
    private const uint WmQuit = 0x0012;

    private readonly ConcurrentQueue<ObservedKeyboardEvent> events = new();
    private readonly AutoResetEvent eventArrived = new(false);
    private readonly ManualResetEventSlim started = new(false);
    private readonly LowLevelKeyboardProc callback;
    private readonly Thread hookThread;
    private IntPtr hook;
    private uint hookThreadId;
    private Exception? startupException;
    private bool disposed;

    public KeyboardEventRecorder()
    {
        callback = HookCallback;
        hookThread = new Thread(RunHook)
        {
            IsBackground = true,
            Name = "Keyboard Manager UI test event recorder",
        };
        hookThread.Start();

        if (!started.Wait(TimeSpan.FromSeconds(10)))
        {
            throw new TimeoutException("The low-level keyboard event recorder did not start within 10 seconds.");
        }

        if (startupException is not null)
        {
            throw new InvalidOperationException("The low-level keyboard event recorder could not start.", startupException);
        }
    }

    public IReadOnlyList<ObservedKeyboardEvent> Snapshot() => events.ToArray();

    public void Clear()
    {
        while (events.TryDequeue(out _))
        {
        }
    }

    public bool WaitForSequence(ulong extraInfo, int timeoutMS, params ExpectedKeyboardEvent[] expected)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMS);
        do
        {
            var generated = Snapshot().Where(item => item.ExtraInfo == extraInfo).ToArray();
            if (ContainsOrderedSequence(generated, expected))
            {
                return true;
            }

            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            eventArrived.WaitOne(remaining > TimeSpan.FromMilliseconds(250) ? 250 : (int)remaining.TotalMilliseconds);
        }
        while (DateTime.UtcNow < deadline);

        return false;
    }

    public string DescribeGeneratedEvents() =>
        string.Join(
            ", ",
            Snapshot()
                .Where(item => item.ExtraInfo != 0)
                .Select(item =>
                    $"t={item.EventTime} vk=0x{item.VirtualKey:X2} {(item.IsKeyDown ? "down" : "up")} extra=0x{item.ExtraInfo:X}"));

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        var stopped = !hookThread.IsAlive;
        if (!stopped && hookThreadId != 0)
        {
            PostThreadMessage(hookThreadId, WmQuit, IntPtr.Zero, IntPtr.Zero);
            stopped = hookThread.Join(TimeSpan.FromSeconds(5));
        }

        if (!stopped && hook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(hook);
            hook = IntPtr.Zero;
            PostThreadMessage(hookThreadId, WmQuit, IntPtr.Zero, IntPtr.Zero);
            stopped = hookThread.Join(TimeSpan.FromSeconds(1));
        }

        if (stopped)
        {
            eventArrived.Dispose();
            started.Dispose();
        }
    }

    private static bool ContainsOrderedSequence(
        IReadOnlyList<ObservedKeyboardEvent> actual,
        IReadOnlyList<ExpectedKeyboardEvent> expected)
    {
        var expectedIndex = 0;
        foreach (var item in actual)
        {
            if (expectedIndex < expected.Count &&
                item.VirtualKey == expected[expectedIndex].VirtualKey &&
                item.IsKeyDown == expected[expectedIndex].IsKeyDown)
            {
                expectedIndex++;
            }
        }

        return expectedIndex == expected.Count;
    }

    private void RunHook()
    {
        try
        {
            hookThreadId = GetCurrentThreadId();
            PeekMessage(out _, IntPtr.Zero, 0, 0, 0);
            hook = SetWindowsHookEx(WhKeyboardLl, callback, GetModuleHandle(null), 0);
            if (hook == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }
        catch (Exception ex)
        {
            startupException = ex;
        }
        finally
        {
            started.Set();
        }

        if (startupException is not null)
        {
            return;
        }

        try
        {
            while (GetMessage(out _, IntPtr.Zero, 0, 0) > 0)
            {
            }
        }
        finally
        {
            if (hook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(hook);
            }
        }
    }

    private IntPtr HookCallback(int code, IntPtr message, IntPtr data)
    {
        if (code >= 0)
        {
            var messageId = unchecked((uint)message.ToInt64());
            var isKeyDown = messageId is WmKeyDown or WmSysKeyDown;
            var isKeyUp = messageId is WmKeyUp or WmSysKeyUp;
            if (isKeyDown || isKeyUp)
            {
                var keyboardData = Marshal.PtrToStructure<KbdLlHookStruct>(data);
                events.Enqueue(new ObservedKeyboardEvent(
                    unchecked((int)keyboardData.VirtualKey),
                    isKeyDown,
                    keyboardData.ExtraInfo.ToUInt64(),
                    keyboardData.Time));
                eventArrived.Set();
            }
        }

        return CallNextHookEx(hook, code, message, data);
    }

    private delegate IntPtr LowLevelKeyboardProc(int code, IntPtr message, IntPtr data);

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct
    {
        public uint VirtualKey;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Message
    {
        public IntPtr Window;
        public uint Id;
        public UIntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public System.Drawing.Point Point;
        public uint Private;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(
        int hookType,
        LowLevelKeyboardProc callback,
        IntPtr module,
        uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetMessage(out Message message, IntPtr window, uint minimumMessage, uint maximumMessage);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PeekMessage(
        out Message message,
        IntPtr window,
        uint minimumMessage,
        uint maximumMessage,
        uint removeMessage);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostThreadMessage(uint threadId, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? moduleName);
}
