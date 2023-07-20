// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using static MouseJumpUI.NativeMethods.Core;

namespace MouseJumpUI.NativeMethods;

internal static partial class User32
{
    /// <summary>
    /// Contains window class information. It is used with the RegisterClassEx and GetClassInfoEx  functions.
    /// </summary>
    /// <remarks>
    /// See https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-wndclassexw
    /// </remarks>
    [SuppressMessage("SA1307", "SA1307:AccessibleFieldsMustBeginWithUpperCaseLetter", Justification = "Names match Win32 api")]
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal readonly struct WNDCLASSEXW
    {
        public readonly UINT cbSize;
        public readonly WNDCLASS_STYLES style;
        public readonly WNDPROC lpfnWndProc;
        public readonly int cbClsExtra;
        public readonly int cbWndExtra;
        public readonly HINSTANCE hInstance;
        public readonly HICON hIcon;
        public readonly HCURSOR hCursor;
        public readonly HBRUSH hbrBackground;
        public readonly PCWSTR lpszMenuName;
        public readonly PCWSTR lpszClassName;
        public readonly HICON hIconSm;

        public WNDCLASSEXW(
            UINT cbSize,
            WNDCLASS_STYLES style,
            [MarshalAs(UnmanagedType.FunctionPtr)]
            WNDPROC lpfnWndProc,
            int cbClsExtra,
            int cbWndExtra,
            HINSTANCE hInstance,
            HICON hIcon,
            HCURSOR hCursor,
            HBRUSH hbrBackground,
            PCWSTR lpszMenuName,
            PCWSTR lpszClassName,
            HICON hIconSm)
        {
            this.cbSize = cbSize;
            this.style = style;
            this.lpfnWndProc = lpfnWndProc;
            this.cbClsExtra = cbClsExtra;
            this.cbWndExtra = cbWndExtra;
            this.hInstance = hInstance;
            this.hIcon = hIcon;
            this.hCursor = hCursor;
            this.hbrBackground = hbrBackground;
            this.lpszMenuName = lpszMenuName;
            this.lpszClassName = lpszClassName;
            this.hIconSm = hIconSm;
        }
    }
}
