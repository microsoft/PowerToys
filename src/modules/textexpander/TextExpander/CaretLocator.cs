// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// Finds the text cursor, for the notice. First the caret Windows keeps for the target window's thread; then, for apps
/// that draw their own, UI Automation's caret in the focused element (its TextPattern2 caret range, or the selection);
/// then the focused element's bottom-left; then the target window's bottom middle; and last the pointer. UI Automation
/// asks the app itself, so it runs on a thread of its own and is given up on after a moment: a hung app must never
/// stall the engine (see <see cref="CaretAutomation"/>).
/// </summary>
internal static class CaretLocator
{
    // Larger than any caret: UI Automation sometimes answers with the bounds of a whole document.
    private const int MaxCaretHeight = 2_000;

    /// <summary>
    /// Gets the caret's rectangle in physical screen pixels, or the fallbacks above. Never an empty rectangle at 0,0.
    /// Takes up to <see cref="CaretAutomation.TimeoutMs"/> when it has to ask UI Automation.
    /// </summary>
    public static Native.RECT Locate(IntPtr targetWindow) => Locate(targetWindow, Sources.System);

    /// <summary>Finds the caret through <paramref name="sources"/>, in the order above.</summary>
    internal static Native.RECT Locate(IntPtr targetWindow, Sources sources)
    {
        // What's found is checked against the window, in case focus moved elsewhere since the insertion.
        Native.RECT? window = sources.Window(targetWindow);
        if (sources.SystemCaret(targetWindow) is { } system && IsCaret(system) && Overlaps(system, window))
        {
            return AsCaret(system);
        }

        // UI Automation's focus is in whichever window is in front, so it's the target's only while the target is: before
        // the query and after it, since the query can take a moment.
        if (sources.InFront(targetWindow) && sources.Automation(targetWindow) is { } focus && sources.InFront(targetWindow))
        {
            if (focus.Caret is { } caret && IsCaret(caret) && Overlaps(caret, window))
            {
                return AsCaret(caret);
            }

            if (focus.Selection is { } selection && IsCaret(selection) && Overlaps(selection, window))
            {
                return AsCaret(selection);
            }

            if (focus.Element is { } element && HasArea(element) && Overlaps(element, window))
            {
                return Point(element.Left, element.Bottom);
            }
        }

        if (window is { } frame && HasArea(frame))
        {
            return Point(frame.Left + ((frame.Right - frame.Left) / 2), frame.Bottom);
        }

        if (sources.Pointer() is { } pointer)
        {
            return Point(pointer.X, pointer.Y);
        }

        Native.RECT work = sources.PrimaryWorkArea();
        return Point(work.Left + ((work.Right - work.Left) / 2), work.Top + ((work.Bottom - work.Top) / 2));
    }

    /// <summary>
    /// Finds the caret in a text range from UI Automation: at the range's edge, its start for a caret range and its end
    /// for a selection. Many apps give an empty range no bounds, so then it's the left edge of the character after it,
    /// or, at the end of the text, the right edge of the one before it. Each is asked for only when needed.
    /// </summary>
    internal static Native.RECT? CaretInRange(Func<double[]?> bounds, Func<double[]?> nextCharacter, Func<double[]?> previousCharacter, bool atEnd)
        => Edge(bounds(), atEnd) ?? Edge(nextCharacter(), atEnd: false) ?? Edge(previousCharacter(), atEnd: true);

    /// <summary>
    /// Gets a caret 1 pixel wide at the leading edge of UI Automation's first rectangle, or the trailing edge of its last.
    /// The rectangles come as left, top, width and height, one after another.
    /// </summary>
    internal static Native.RECT? Edge(double[]? rectangles, bool atEnd)
    {
        if (rectangles is null || rectangles.Length < 4)
        {
            return null;
        }

        int first = atEnd ? ((rectangles.Length / 4) - 1) * 4 : 0;
        double left = rectangles[first];
        double top = rectangles[first + 1];
        double width = rectangles[first + 2];
        double height = rectangles[first + 3];
        if (!OnScreen(left) || !OnScreen(top) || !OnScreen(width) || !OnScreen(height) || height <= 0)
        {
            return null;
        }

        int x = (int)Math.Floor(atEnd ? left + width : left);
        return new Native.RECT { Left = x, Top = (int)Math.Floor(top), Right = x + 1, Bottom = (int)Math.Ceiling(top + height) };
    }

    private static bool OnScreen(double value) => double.IsFinite(value) && Math.Abs(value) < 1_000_000;

    private static bool IsCaret(Native.RECT caret)
        => caret.Bottom > caret.Top && caret.Bottom - caret.Top <= MaxCaretHeight && caret.Right >= caret.Left;

    private static bool HasArea(Native.RECT rect) => rect.Right > rect.Left && rect.Bottom > rect.Top;

    // A caret drawn as a line is 0 pixels wide; it counts as 1 here, so one just past the window's edge doesn't overlap.
    private static bool Overlaps(Native.RECT rect, Native.RECT? window)
        => window is not { } frame
            || (rect.Left < frame.Right && Math.Max(rect.Right, rect.Left + 1) > frame.Left && rect.Top < frame.Bottom && rect.Bottom > frame.Top);

    private static bool IsInFront(IntPtr targetWindow)
    {
        if (targetWindow == IntPtr.Zero)
        {
            return true;
        }

        IntPtr foreground = Native.GetForegroundWindow();
        return foreground != IntPtr.Zero && Native.GetAncestor(foreground, Native.GA_ROOT) == Native.GetAncestor(targetWindow, Native.GA_ROOT);
    }

    // A caret drawn as a line can be 0 pixels wide; it's 1 here, so the rectangle is never empty.
    private static Native.RECT AsCaret(Native.RECT caret)
    {
        if (caret.Right <= caret.Left)
        {
            caret.Right = caret.Left + 1;
        }

        return caret;
    }

    // A point, as a 1-pixel rectangle above it, so the notice goes just under the point or just over it.
    private static Native.RECT Point(int x, int y) => new() { Left = x, Top = y - 1, Right = x + 1, Bottom = y };

    // rcCaret is in the caret window's client coordinates as that window sees them: logical pixels when Windows scales
    // the app. So it's read and mapped to the screen in the window's own DPI context, then converted to physical pixels.
    private static Native.RECT? SystemCaret(IntPtr targetWindow)
    {
        uint thread = targetWindow == IntPtr.Zero ? 0 : Native.GetWindowThreadProcessId(targetWindow, out _);
        if (targetWindow != IntPtr.Zero && thread == 0)
        {
            return null;
        }

        var info = new Native.GUITHREADINFO { CbSize = (uint)Marshal.SizeOf<Native.GUITHREADINFO>() };
        if (!Native.GetGUIThreadInfo(thread, ref info) || info.HwndCaret == IntPtr.Zero || !Native.IsWindowVisible(info.HwndCaret))
        {
            return null;
        }

        IntPtr caretWindow = info.HwndCaret;
        IntPtr previous = Native.SetThreadDpiAwarenessContext(Native.GetWindowDpiAwarenessContext(caretWindow));
        try
        {
            if (!Native.GetGUIThreadInfo(thread, ref info) || info.HwndCaret != caretWindow)
            {
                return null;
            }

            var topLeft = new Native.POINT { X = info.RcCaret.Left, Y = info.RcCaret.Top };
            var bottomRight = new Native.POINT { X = info.RcCaret.Right, Y = info.RcCaret.Bottom };
            if (!Native.ClientToScreen(caretWindow, ref topLeft) || !Native.ClientToScreen(caretWindow, ref bottomRight))
            {
                return null;
            }

            _ = Native.LogicalToPhysicalPointForPerMonitorDPI(caretWindow, ref topLeft);
            _ = Native.LogicalToPhysicalPointForPerMonitorDPI(caretWindow, ref bottomRight);
            return new Native.RECT { Left = topLeft.X, Top = topLeft.Y, Right = bottomRight.X, Bottom = bottomRight.Y };
        }
        finally
        {
            if (previous != IntPtr.Zero)
            {
                _ = Native.SetThreadDpiAwarenessContext(previous);
            }
        }
    }

    // The visible frame: without the invisible resize border, the bottom middle is on the window's edge.
    private static Native.RECT? WindowFrame(IntPtr targetWindow)
    {
        IntPtr window = targetWindow != IntPtr.Zero ? targetWindow : Native.GetForegroundWindow();
        if (window == IntPtr.Zero || !Native.IsWindow(window) || Native.IsIconic(window))
        {
            return null;
        }

        if (Native.DwmGetWindowAttribute(window, Native.DWMWA_EXTENDED_FRAME_BOUNDS, out Native.RECT frame, Marshal.SizeOf<Native.RECT>()) >= 0 && HasArea(frame))
        {
            return frame;
        }

        return Native.GetWindowRect(window, out Native.RECT bounds) ? bounds : null;
    }

    private static Native.POINT? PointerPosition() => Native.GetCursorPos(out Native.POINT pointer) ? pointer : null;

    private static Native.RECT PrimaryWorkArea()
    {
        var info = new Native.MONITORINFO { CbSize = (uint)Marshal.SizeOf<Native.MONITORINFO>() };
        IntPtr primary = Native.MonitorFromPoint(default, Native.MONITOR_DEFAULTTOPRIMARY);
        return Native.GetMonitorInfoW(primary, ref info) ? info.RcWork : new Native.RECT { Right = 1024, Bottom = 768 };
    }

    /// <summary>What UI Automation found in the focused element, in physical screen pixels.</summary>
    /// <param name="Caret">From its TextPattern2 caret range.</param>
    /// <param name="Selection">From its selection, asked for only without a caret range.</param>
    /// <param name="Element">Its bounds.</param>
    internal readonly record struct Focus(Native.RECT? Caret, Native.RECT? Selection, Native.RECT? Element);

    /// <summary>Where each step looks, so tests can stand in for Windows and the app.</summary>
    internal sealed record Sources(
        Func<IntPtr, Native.RECT?> SystemCaret,
        Func<IntPtr, bool> InFront,
        Func<IntPtr, Focus?> Automation,
        Func<IntPtr, Native.RECT?> Window,
        Func<Native.POINT?> Pointer,
        Func<Native.RECT> PrimaryWorkArea)
    {
        public static Sources System { get; } = new(
            CaretLocator.SystemCaret,
            IsInFront,
            static _ => CaretAutomation.Find(),
            WindowFrame,
            PointerPosition,
            CaretLocator.PrimaryWorkArea);
    }
}
