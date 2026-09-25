// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using Microsoft.CmdPal.UI.Utilities;

namespace Microsoft.CmdPal.Common.UnitTests.Helpers;

[TestClass]
[DoNotParallelize]
public class TaskbarZOrderTests
{
    [TestMethod]
    public void RepairsRepeatedTaskbarRaisesWithoutLayoutEvents()
    {
        using var taskbar = new TestWindow();
        using var overlay = new TestWindow();

        for (var i = 0; i < 25; i++)
        {
            taskbar.Raise();
            Assert.IsTrue(IsAbove(taskbar.Handle, overlay.Handle));
            Assert.IsTrue(TaskbarZOrder.EnsureAboveTaskbar(overlay.Handle, taskbar.Handle));
            Assert.IsTrue(IsAbove(overlay.Handle, taskbar.Handle));
            Assert.IsFalse(TaskbarZOrder.EnsureAboveTaskbar(overlay.Handle, taskbar.Handle));
        }
    }

    [TestMethod]
    public void CorrectOrderDoesNotRaiseOverlayAbovePopup()
    {
        using var taskbar = new TestWindow();
        using var overlay = new TestWindow();
        using var popup = new TestWindow();

        Assert.IsFalse(TaskbarZOrder.EnsureAboveTaskbar(overlay.Handle, taskbar.Handle));
        Assert.IsTrue(IsAbove(popup.Handle, overlay.Handle));
        Assert.IsTrue(IsAbove(overlay.Handle, taskbar.Handle));
    }

    [TestMethod]
    public void RepairDoesNotActivateMoveOrResizeOverlay()
    {
        using var overlay = new TestWindow();
        using var taskbar = new TestWindow();
        var foreground = NativeMethods.GetForegroundWindow();
        Assert.IsTrue(NativeMethods.GetWindowRect(overlay.Handle, out var before));

        Assert.IsTrue(TaskbarZOrder.EnsureAboveTaskbar(overlay.Handle, taskbar.Handle));

        Assert.AreEqual(foreground, NativeMethods.GetForegroundWindow());
        Assert.IsTrue(NativeMethods.GetWindowRect(overlay.Handle, out var after));
        Assert.AreEqual(before, after);
    }

    [TestMethod]
    public void HiddenOverlayRemainsHidden()
    {
        using var overlay = new TestWindow();
        using var taskbar = new TestWindow();
        overlay.Hide();

        Assert.IsFalse(TaskbarZOrder.EnsureAboveTaskbar(overlay.Handle, taskbar.Handle));
        Assert.IsFalse(NativeMethods.IsWindowVisible(overlay.Handle));
    }

    [TestMethod]
    public void HiddenTaskbarDoesNotRaiseOverlay()
    {
        using var overlay = new TestWindow();
        using var taskbar = new TestWindow();
        taskbar.Hide();

        Assert.IsFalse(TaskbarZOrder.EnsureAboveTaskbar(overlay.Handle, taskbar.Handle));
    }

    [TestMethod]
    public void MissingTaskbarIsIgnoredAndReplacementIsHandled()
    {
        using var overlay = new TestWindow();
        Assert.IsFalse(TaskbarZOrder.EnsureAboveTaskbar(overlay.Handle, 0));

        using var taskbar = new TestWindow();
        Assert.IsTrue(TaskbarZOrder.EnsureAboveTaskbar(overlay.Handle, taskbar.Handle));
        Assert.IsTrue(IsAbove(overlay.Handle, taskbar.Handle));
    }

    private static bool IsAbove(nint above, nint below)
    {
        for (var hwnd = NativeMethods.GetWindow(below, 3); hwnd != 0; hwnd = NativeMethods.GetWindow(hwnd, 3))
        {
            if (hwnd == above)
            {
                return true;
            }
        }

        return false;
    }

    private sealed class TestWindow : IDisposable
    {
        public nint Handle { get; } = NativeMethods.CreateWindowEx(
            0x08000088, // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST
            "STATIC",
            "TaskbarZOrderTests",
            0x80000000, // WS_POPUP
            -32000,
            -32000,
            10,
            10,
            0,
            0,
            0,
            0);

        public TestWindow()
        {
            Assert.AreNotEqual((nint)0, Handle);
            Raise();
        }

        public void Raise() => Assert.IsTrue(NativeMethods.SetWindowPos(
            Handle,
            -1, // HWND_TOPMOST
            0,
            0,
            0,
            0,
            0x0053)); // SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW

        public void Hide() => NativeMethods.ShowWindow(Handle, 0);

        public void Dispose() => Assert.IsTrue(NativeMethods.DestroyWindow(Handle));
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern nint CreateWindowEx(uint exStyle, string className, string windowName, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint param);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DestroyWindow(nint hwnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int width, int height, uint flags);

        [DllImport("user32.dll")]
        public static extern nint GetWindow(nint hwnd, uint command);

        [DllImport("user32.dll")]
        public static extern nint GetForegroundWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(nint hwnd, out WindowRect rect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ShowWindow(nint hwnd, int command);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(nint hwnd);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
