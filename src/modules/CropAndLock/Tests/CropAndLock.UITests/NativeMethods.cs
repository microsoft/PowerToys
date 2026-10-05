// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.Win32.SafeHandles;

namespace Microsoft.CropAndLock.UITests
{
    internal static class NativeMethods
    {
        internal const long ChildStyle = 0x40000000;
        internal const long TopmostStyle = 0x00000008;

        private const uint GaRoot = 2;
        private const uint ProcessQueryLimitedInformation = 0x1000;
        private const int ErrorInsufficientBuffer = 122;
        private const int AppModelErrorNoPackage = 15700;
        private const int GwlStyle = -16;
        private const int GwlExStyle = -20;

        internal readonly record struct WindowState(IntPtr Parent, long Style, long ExtendedStyle, Rectangle Bounds);

        internal static Rectangle ClientBounds(IntPtr window)
        {
            if (!GetClientRect(window, out var rect) ||
                !ClientToScreen(window, ref rect.LeftTop) ||
                !ClientToScreen(window, ref rect.RightBottom))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            return Rectangle.FromLTRB(rect.LeftTop.X, rect.LeftTop.Y, rect.RightBottom.X, rect.RightBottom.Y);
        }

        internal static WindowState ReadState(IntPtr window)
        {
            var (left, top, right, bottom) = WindowHelper.GetWindowBounds(window);
            return new WindowState(
                GetParent(window),
                ReadWindowLong(window, GwlStyle),
                ReadWindowLong(window, GwlExStyle),
                Rectangle.FromLTRB(left, top, right, bottom));
        }

        // WindowControl/WindowHelper have private equivalents of these raw queries.
        // Keep them local until the shared harness exposes window identity and style snapshots.
        internal static string ClassName(IntPtr window)
        {
            var name = new StringBuilder(256);
            _ = GetClassNameW(window, name, name.Capacity);
            return name.ToString();
        }

        internal static int ProcessId(IntPtr window)
        {
            _ = GetWindowThreadProcessId(window, out var processId);
            return (int)processId;
        }

        internal static IntPtr Root(IntPtr window) => GetAncestor(window, GaRoot);

        internal static IntPtr RootAtPoint(Point point) =>
            Root(WindowFromPoint(new NativePoint { X = point.X, Y = point.Y }));

        internal static string? PackageFullName(int processId)
        {
            using var process = OpenProcess(ProcessQueryLimitedInformation, false, processId);
            if (process.IsInvalid)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            uint length = 0;
            var result = GetPackageFullName(process, ref length, null);
            if (result == AppModelErrorNoPackage)
            {
                return null; // APPMODEL_ERROR_NO_PACKAGE is the Win32 fixture's expected identity.
            }

            if (result != ErrorInsufficientBuffer)
            {
                throw new Win32Exception(result);
            }

            var name = new StringBuilder((int)length);
            result = GetPackageFullName(process, ref length, name);
            if (result != 0)
            {
                throw new Win32Exception(result);
            }

            return name.ToString();
        }

        internal static IReadOnlyList<int> ProcessIds(string name)
        {
            var processes = System.Diagnostics.Process.GetProcessesByName(name);
            try
            {
                return processes.Select(process => process.Id).ToArray();
            }
            finally
            {
                foreach (var process in processes)
                {
                    process.Dispose();
                }
            }
        }

        private static long ReadWindowLong(IntPtr window, int index)
        {
            Marshal.SetLastSystemError(0);
            var value = GetWindowLongPtrW(window, index);
            var error = Marshal.GetLastPInvokeError();
            if (value == IntPtr.Zero && error != 0)
            {
                throw new Win32Exception(error, $"Could not read window style {index} for HWND 0x{window:X}.");
            }

            return value.ToInt64();
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public NativePoint LeftTop;
            public NativePoint RightBottom;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWindow(IntPtr window);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetParent(IntPtr window);

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr window, uint flags);

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(NativePoint point);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetClientRect(IntPtr window, out NativeRect rectangle);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ClientToScreen(IntPtr window, ref NativePoint point);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr GetWindowLongPtrW(IntPtr window, int index);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int GetClassNameW(IntPtr window, StringBuilder className, int count);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, int processId);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int GetPackageFullName(SafeProcessHandle process, ref uint length, StringBuilder? name);
    }
}
