// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Microsoft.PowerToys.ZoomIt.UITests;

internal sealed class KeyboardInputObserver : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const uint WmQuit = 0x0012;
    private const uint WmKeyDown = 0x0100;
    private const uint WmKeyUp = 0x0101;
    private const uint WmSysKeyDown = 0x0104;
    private const uint WmSysKeyUp = 0x0105;
    private const uint LlkhfInjected = 0x10;
    private const uint VkBack = 0x08;
    private const uint VkSpace = 0x20;
    private const uint VkPacket = 0xE7;

    private readonly IntPtr targetWindow;
    private readonly ObservedInput[] events = new ObservedInput[256];
    private readonly ManualResetEventSlim started = new(false);
    private readonly LowLevelKeyboardProc callback;
    private readonly Thread hookThread;
    private IntPtr hook;
    private uint hookThreadId;
    private int stopRequested;
    private int eventCount;
    private int overflowed;
    private Exception? failure;

    public KeyboardInputObserver(IntPtr targetWindow)
    {
        if (targetWindow == IntPtr.Zero)
        {
            throw new ArgumentException("An owned foreground window is required.", nameof(targetWindow));
        }

        this.targetWindow = targetWindow;
        callback = ObserveInput;
        hookThread = new Thread(RunHook)
        {
            IsBackground = true,
            Name = "ZoomIt Demo Type input observer",
        };
        hookThread.Start();

        try
        {
            if (!started.Wait(TimeSpan.FromSeconds(10)))
            {
                throw new TimeoutException("The Demo Type input observer did not start within 10 seconds.");
            }

            if (Failure is not null)
            {
                throw new InvalidOperationException("The Demo Type input observer could not start.", Failure);
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public Exception? Failure => Volatile.Read(ref failure);

    public bool Overflowed => Volatile.Read(ref overflowed) != 0;

    public ObservedInput[] Snapshot()
    {
        var count = Volatile.Read(ref eventCount);
        var result = new ObservedInput[count];
        Array.Copy(events, result, count);
        return result;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref stopRequested, 1) != 0)
        {
            return;
        }

        RequestExit();
        var stopped = hookThread.Join(TimeSpan.FromSeconds(5));
        if (!stopped)
        {
            RemoveHook();
            RequestExit();
            stopped = hookThread.Join(TimeSpan.FromSeconds(1));
        }

        if (!stopped)
        {
            // The live thread retains this instance and its callback if shutdown times out.
            throw new TimeoutException("The Demo Type input observer did not stop within 6 seconds.");
        }

        started.Dispose();
        GC.KeepAlive(callback);
    }

    private void RequestExit()
    {
        var threadId = Volatile.Read(ref hookThreadId);
        if (threadId != 0)
        {
            PostThreadMessageW(threadId, WmQuit, UIntPtr.Zero, IntPtr.Zero);
        }
    }

    private void RunHook()
    {
        try
        {
            // Create the queue before publishing its ID so early disposal cannot lose WM_QUIT.
            PeekMessageW(out _, IntPtr.Zero, 0, 0, 0);
            Volatile.Write(ref hookThreadId, GetCurrentThreadId());
            if (Volatile.Read(ref stopRequested) == 0)
            {
                var installed = SetWindowsHookExW(WhKeyboardLl, callback, GetModuleHandleW(null), 0);
                if (installed == IntPtr.Zero)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not install the Demo Type input observer.");
                }

                Interlocked.Exchange(ref hook, installed);
            }

            started.Set();
            while (Volatile.Read(ref stopRequested) == 0)
            {
                var result = GetMessageW(out _, IntPtr.Zero, 0, 0);
                if (result == -1)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "The Demo Type input observer's message loop failed.");
                }

                if (result == 0)
                {
                    break;
                }
            }

            if (Volatile.Read(ref stopRequested) == 0)
            {
                throw new InvalidOperationException("The Demo Type input observer stopped unexpectedly.");
            }
        }
        catch (Exception exception)
        {
            Interlocked.CompareExchange(ref failure, exception, null);
        }
        finally
        {
            RemoveHook();
            started.Set();
            GC.KeepAlive(callback);
        }
    }

    private void RemoveHook()
    {
        var installed = Interlocked.Exchange(ref hook, IntPtr.Zero);
        if (installed != IntPtr.Zero && !UnhookWindowsHookEx(installed))
        {
            Interlocked.CompareExchange(
                ref failure,
                new Win32Exception(Marshal.GetLastWin32Error(), "Could not remove the Demo Type input observer."),
                null);
        }
    }

    private IntPtr ObserveInput(int code, UIntPtr message, IntPtr data)
    {
        try
        {
            var messageId = unchecked((uint)message.ToUInt64());
            if (code >= 0 && messageId is WmKeyDown or WmKeyUp or WmSysKeyDown or WmSysKeyUp)
            {
                var input = Marshal.PtrToStructure<KeyboardInput>(data);
                if ((input.Flags & LlkhfInjected) != 0 &&
                    input.VirtualKey is VkBack or VkSpace or VkPacket &&
                    GetForegroundWindow() == targetWindow)
                {
                    // Only this thread writes; publish complete entries without locks or callback logging.
                    var index = eventCount;
                    if (index < events.Length)
                    {
                        events[index] = new ObservedInput(
                            input.VirtualKey,
                            input.ScanCode,
                            messageId is WmKeyDown or WmSysKeyDown,
                            input.Flags,
                            input.Time);
                        Volatile.Write(ref eventCount, index + 1);
                    }
                    else
                    {
                        Volatile.Write(ref overflowed, 1);
                    }
                }
            }
        }
        catch (Exception exception)
        {
            Interlocked.CompareExchange(ref failure, exception, null);
        }

        return CallNextHookEx(IntPtr.Zero, code, message, data);
    }

    internal readonly record struct ObservedInput(uint VirtualKey, uint ScanCode, bool IsKeyDown, uint Flags, uint Time)
    {
        public bool IsProbeKey => VirtualKey is VkSpace or VkBack;

        public bool IsUnicodePacket => VirtualKey == VkPacket;

        public override string ToString() => $"t={Time} vk=0x{VirtualKey:X2} scan=0x{ScanCode:X4} {(IsKeyDown ? "down" : "up")} flags=0x{Flags:X2}";
    }

    private delegate IntPtr LowLevelKeyboardProc(int code, UIntPtr message, IntPtr data);

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
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

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern IntPtr SetWindowsHookExW(int hookType, LowLevelKeyboardProc callback, IntPtr module, uint threadId);

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, UIntPtr message, IntPtr data);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern int GetMessageW(out Message message, IntPtr window, uint minimumMessage, uint maximumMessage);

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PeekMessageW(out Message message, IntPtr window, uint minimumMessage, uint maximumMessage, uint removeMessage);

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostThreadMessageW(uint threadId, uint message, UIntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern uint GetCurrentThreadId();

    [DllImport("kernel32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string? moduleName);
}
