// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace PowerToys.TryRun;

internal static partial class WindowBorderNative
{
    internal const int ExtendedStyle = -20;
    internal const long Transparent = 0x20;
    internal const long ToolWindow = 0x80;
    internal const long NoActivate = 0x08000000;
    internal const uint NoActivation = 0x0010;

    internal delegate bool EnumWindowCallback(nint window, nint parameter);

    [StructLayout(LayoutKind.Sequential)]
    internal struct Rectangle : IEquatable<Rectangle>
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;

        public readonly bool Equals(Rectangle other) => Left == other.Left && Top == other.Top && Right == other.Right && Bottom == other.Bottom;

        public override readonly bool Equals(object? obj) => obj is Rectangle other && Equals(other);

        public override readonly int GetHashCode() => HashCode.Combine(Left, Top, Right, Bottom);
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool EnumWindows(EnumWindowCallback callback, nint parameter);

    [LibraryImport("user32.dll")]
    internal static partial uint GetWindowThreadProcessId(nint window, out uint processId);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsWindowVisible(nint window);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsIconic(nint window);

    [LibraryImport("user32.dll")]
    internal static partial nint GetWindow(nint window, uint command);

    [LibraryImport("user32.dll")]
    internal static partial nint GetWindowLongPtrW(nint window, int index);

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial nint SetWindowLongPtrW(nint window, int index, nint value);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetWindowRect(nint window, out Rectangle rectangle);

    [LibraryImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")]
    internal static partial int GetFrameBounds(nint window, uint attribute, out Rectangle rectangle, uint size);

    [LibraryImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")]
    internal static partial int GetCloaked(nint window, uint attribute, out uint cloaked, uint size);
}
