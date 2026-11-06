// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Text;

using EditorNativeMethods = EnvironmentVariables.Win32.NativeMethods;

namespace EnvironmentVariablesUILib.UnitTests.Helpers;

[TestClass]
public class WindowProcedureTests
{
    [TestMethod]
    [DataRow("Environment Variables")]
    [DataRow("环境变量")]
    [DataRow("管理员: 环境变量")]
    public void SubclassedWindowPreservesUnicodeTitle(string title)
    {
        // A hidden native window exercises the editor's actual P/Invokes without starting WinUI.
        var window = CreateWindowExW(0, "STATIC", title, 0, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        Assert.AreNotEqual(IntPtr.Zero, window);

        IntPtr previousProcedure = IntPtr.Zero;
        EditorNativeMethods.WinProc procedure = (handle, message, wParam, lParam) =>
            EditorNativeMethods.CallWindowProc(previousProcedure, handle, message, wParam, lParam);

        try
        {
            previousProcedure = EditorNativeMethods.SetWindowLongPtr(window, EditorNativeMethods.WindowLongIndexFlags.GWL_WNDPROC, procedure);
            Assert.AreNotEqual(IntPtr.Zero, previousProcedure);

            // This also detects the regression on machines that do not use the UTF-8 system code page.
            Assert.IsTrue(IsWindowUnicode(window), "Subclassing must not turn the WinUI window into an ANSI window.");

            var length = GetWindowTextLengthW(window);
            Assert.AreEqual(title.Length, length);
            var text = new StringBuilder(length + 1);
            Assert.AreEqual(length, GetWindowTextW(window, text, text.Capacity));
            Assert.AreEqual(title, text.ToString());
        }
        finally
        {
            DestroyWindow(window);
            GC.KeepAlive(procedure);
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern IntPtr CreateWindowExW(uint extendedStyle, string className, string windowName, uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr parameter);

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowUnicode(IntPtr window);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern int GetWindowTextLengthW(IntPtr window);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int GetWindowTextW(IntPtr window, StringBuilder text, int maximumCount);

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(IntPtr window);
}
