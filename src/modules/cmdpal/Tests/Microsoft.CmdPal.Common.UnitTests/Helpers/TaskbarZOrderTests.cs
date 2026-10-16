// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
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

    [TestMethod]
    public void ChildOverlayDoesNotGetReorderedAsATopLevelWindow()
    {
        using var parent = new TestWindow();
        using var taskbar = new TestWindow();
        using var overlay = new TestWindow();
        using var promotion = new TaskbarBandPromotion(overlay.Handle, parent.Handle);
        taskbar.Raise();

        Assert.IsFalse(TaskbarZOrder.EnsureAboveTaskbar(overlay.Handle, taskbar.Handle));
        Assert.AreEqual(parent.Handle, NativeMethods.GetParent(overlay.Handle));
    }

    [TestMethod]
    public void ChildTaskbarDoesNotGetComparedWithTopLevelOverlay()
    {
        using var parent = new TestWindow();
        using var overlay = new TestWindow();
        using var taskbar = new TestWindow();
        using var promotion = new TaskbarBandPromotion(taskbar.Handle, parent.Handle);

        Assert.IsFalse(TaskbarZOrder.EnsureAboveTaskbar(overlay.Handle, taskbar.Handle));
    }

    [TestMethod]
    public void MissingOverlayDoesNotRaiseAnotherWindow()
    {
        using var taskbar = new TestWindow();

        Assert.IsFalse(TaskbarZOrder.EnsureAboveTaskbar(0, taskbar.Handle));
    }

    [TestMethod]
    public void TemporaryParentPreservesScreenBoundsAndRestoresStyles()
    {
        using var taskbar = new TestWindow();
        using var overlay = new TestWindow(noActivate: false);
        Assert.IsTrue(NativeMethods.SetWindowPos(taskbar.Handle, 0, -31000, -31000, 100, 100, 0x14));
        var foreground = NativeMethods.GetForegroundWindow();
        var style = NativeMethods.GetWindowLong(overlay.Handle, -16);
        var extendedStyle = NativeMethods.GetWindowLong(overlay.Handle, -20);
        Assert.IsTrue(NativeMethods.GetWindowRect(overlay.Handle, out var before));

        using (var promotion = new TaskbarBandPromotion(overlay.Handle, taskbar.Handle))
        {
            Assert.AreEqual(taskbar.Handle, NativeMethods.GetParent(overlay.Handle));
            Assert.IsTrue((NativeMethods.GetWindowLong(overlay.Handle, -16) & 0x40000000) != 0);
            Assert.IsTrue((NativeMethods.GetWindowLong(overlay.Handle, -20) & 0x08000000) != 0);
            Assert.IsTrue(NativeMethods.GetWindowRect(overlay.Handle, out var attached));
            Assert.AreEqual(before, attached);
            Assert.AreEqual(foreground, NativeMethods.GetForegroundWindow());
        }

        Assert.AreEqual((nint)0, NativeMethods.GetParent(overlay.Handle));
        Assert.AreEqual((nint)0, NativeMethods.GetWindow(overlay.Handle, 4));
        Assert.AreEqual(style, NativeMethods.GetWindowLong(overlay.Handle, -16));
        Assert.AreEqual(extendedStyle, NativeMethods.GetWindowLong(overlay.Handle, -20));
        Assert.IsTrue(NativeMethods.GetWindowRect(overlay.Handle, out var after));
        Assert.AreEqual(before, after);
        Assert.AreEqual(foreground, NativeMethods.GetForegroundWindow());
    }

    [TestMethod]
    public void CancellingPromotionIsIdempotent()
    {
        using var taskbar = new TestWindow();
        using var overlay = new TestWindow();
        var promotion = new TaskbarBandPromotion(overlay.Handle, taskbar.Handle);

        promotion.Dispose();
        promotion.Dispose();

        Assert.AreEqual((nint)0, NativeMethods.GetParent(overlay.Handle));
        Assert.IsTrue(promotion.ShouldRestore);
    }

    [TestMethod]
    public void HiddenDuringPromotionRemainsHiddenAfterDetach()
    {
        using var taskbar = new TestWindow();
        using var overlay = new TestWindow();
        using var promotion = new TaskbarBandPromotion(overlay.Handle, taskbar.Handle);
        overlay.Hide();

        Assert.IsTrue(promotion.ShouldRestore);
        promotion.Dispose();

        Assert.IsFalse(NativeMethods.IsWindowVisible(overlay.Handle));
        Assert.AreEqual((nint)0, NativeMethods.GetParent(overlay.Handle));
    }

    [TestMethod]
    public void HidingTaskbarCancelsPromotion()
    {
        using var taskbar = new TestWindow();
        using var overlay = new TestWindow();
        using var promotion = new TaskbarBandPromotion(overlay.Handle, taskbar.Handle);
        taskbar.Hide();

        Assert.IsTrue(promotion.ShouldRestore);
    }

    [TestMethod]
    public void MissingParentDoesNotChangeOverlay()
    {
        using var overlay = new TestWindow();
        var style = NativeMethods.GetWindowLong(overlay.Handle, -16);
        var extendedStyle = NativeMethods.GetWindowLong(overlay.Handle, -20);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
        {
            using var promotion = new TaskbarBandPromotion(overlay.Handle, 0);
        });

        Assert.AreEqual((nint)0, NativeMethods.GetParent(overlay.Handle));
        Assert.AreEqual(style, NativeMethods.GetWindowLong(overlay.Handle, -16));
        Assert.AreEqual(extendedStyle, NativeMethods.GetWindowLong(overlay.Handle, -20));
    }

    [TestMethod]
    public void ExistingParentIsNotReplaced()
    {
        using var taskbar = new TestWindow();
        using var otherTaskbar = new TestWindow();
        using var overlay = new TestWindow();
        using var promotion = new TaskbarBandPromotion(overlay.Handle, taskbar.Handle);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
        {
            using var replacement = new TaskbarBandPromotion(overlay.Handle, otherTaskbar.Handle);
        });
        Assert.AreEqual(taskbar.Handle, NativeMethods.GetParent(overlay.Handle));
    }

    [TestMethod]
    public void OrdinaryForegroundDoesNotStartPromotion()
    {
        using var taskbar = new TestWindow();
        using var overlay = new TestWindow();

        Assert.IsNull(TaskbarBandPromotion.TryBegin(overlay.Handle, taskbar.Handle, (nint)0));
        Assert.IsNull(TaskbarBandPromotion.TryBegin(overlay.Handle, taskbar.Handle, NativeMethods.GetForegroundWindow()));
        Assert.AreEqual((nint)0, NativeMethods.GetParent(overlay.Handle));
    }

    [TestMethod]
    public void PromotionBudgetExpiresAtOneHundredMilliseconds()
    {
        Assert.IsFalse(TaskbarBandPromotion.ShouldRestoreForBand(1, TimeSpan.FromMilliseconds(99.999)));
        Assert.IsTrue(TaskbarBandPromotion.ShouldRestoreForBand(1, TimeSpan.FromMilliseconds(100)));
        Assert.IsTrue(TaskbarBandPromotion.ShouldRestoreForBand(1, TimeSpan.FromSeconds(1)));
        Assert.IsTrue(TaskbarBandPromotion.ShouldRestoreForBand(6, TimeSpan.Zero));
    }

    [TestMethod]
    public void UnpromotedAttachmentExpiresAndCanBeRestored()
    {
        using var taskbar = new TestWindow();
        using var overlay = new TestWindow();
        using var promotion = new TaskbarBandPromotion(overlay.Handle, taskbar.Handle);

        Thread.Sleep(110);

        Assert.IsTrue(promotion.ShouldRestore);
        promotion.Dispose();
        Assert.AreEqual((nint)0, NativeMethods.GetParent(overlay.Handle));
    }

    [TestMethod]
    public void RestorationRejectsTheWrongThread()
    {
        using var taskbar = new TestWindow();
        using var overlay = new TestWindow();
        using var promotion = new TaskbarBandPromotion(overlay.Handle, taskbar.Handle);

        Task.Run(() => Assert.ThrowsExactly<InvalidOperationException>(promotion.Dispose)).GetAwaiter().GetResult();

        Assert.AreEqual(taskbar.Handle, NativeMethods.GetParent(overlay.Handle));
        promotion.Dispose();
        Assert.AreEqual((nint)0, NativeMethods.GetParent(overlay.Handle));
    }

    [TestMethod]
    public void DifferentDpiAwarenessDoesNotResetOrParentTheOverlay()
    {
        var originalContext = NativeMethods.SetThreadDpiAwarenessContext(-1);
        Assert.AreNotEqual((nint)0, originalContext);
        try
        {
            using var taskbar = new TestWindow();
            Assert.AreNotEqual((nint)0, NativeMethods.SetThreadDpiAwarenessContext(-4));
            using var overlay = new TestWindow();
            var context = NativeMethods.GetWindowDpiAwarenessContext(overlay.Handle);

            Assert.ThrowsExactly<InvalidOperationException>(() =>
            {
                using var promotion = new TaskbarBandPromotion(overlay.Handle, taskbar.Handle);
            });

            Assert.AreEqual((nint)0, NativeMethods.GetParent(overlay.Handle));
            Assert.AreEqual(context, NativeMethods.GetWindowDpiAwarenessContext(overlay.Handle));
        }
        finally
        {
            NativeMethods.SetThreadDpiAwarenessContext(originalContext);
        }
    }

    [TestMethod]
    [DataRow("Windows.UI.Core.CoreWindow", 6u, true)]
    [DataRow("Windows.UI.Core.CoreWindow", 1u, false)]
    [DataRow("XamlExplorerHostIslandWindow", 5u, true)]
    [DataRow("ControlCenterWindow", 4u, true)]
    [DataRow("ControlCenterWindow", 6u, true)]
    [DataRow("UnrelatedWindow", 6u, false)]
    [DataRow("", 6u, false)]
    public void OnlyShellSurfacesInShellBandsAreEligible(string className, uint band, bool expected)
    {
        Assert.AreEqual(expected, TaskbarBandPromotion.IsShellSurface(className, band));
    }

    private static bool IsAbove(nint above, nint below)
    {
        var desktop = NativeMethods.GetDesktopWindow();
        for (var hwnd = NativeMethods.FindWindowEx(desktop, 0, null, null);
            hwnd != 0;
            hwnd = NativeMethods.FindWindowEx(desktop, hwnd, null, null))
        {
            if (hwnd == above)
            {
                return true;
            }

            if (hwnd == below)
            {
                return false;
            }
        }

        return false;
    }

    private sealed class TestWindow : IDisposable
    {
        public nint Handle { get; }

        public TestWindow(bool noActivate = true)
        {
            Handle = NativeMethods.CreateWindowEx(
                0x88 | (noActivate ? 0x08000000u : 0), // WS_EX_TOOLWINDOW | WS_EX_TOPMOST | optional WS_EX_NOACTIVATE
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
        public static extern nint GetDesktopWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern nint FindWindowEx(nint parent, nint childAfter, string? className, string? windowName);

        [DllImport("user32.dll")]
        public static extern nint GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern nint GetParent(nint hwnd);

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(nint hwnd, int index);

        [DllImport("user32.dll")]
        public static extern nint SetThreadDpiAwarenessContext(nint context);

        [DllImport("user32.dll")]
        public static extern nint GetWindowDpiAwarenessContext(nint hwnd);

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
