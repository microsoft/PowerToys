// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// Picks the screen a question or choice window opens on: the one with the caret in the window being typed into, as the
/// notice's (see <see cref="CaretLocator"/>); that window's own screen when the caret isn't found; and the pointer's once
/// the window has gone. The caret is looked for only when the window reaches onto another screen, since finding it can
/// take a moment.
/// </summary>
internal static class PromptScreen
{
    /// <summary>What a screen is picked by.</summary>
    internal enum Basis
    {
        /// <summary>The caret, at <see cref="Choice.Point"/>.</summary>
        Caret,

        /// <summary>The window being typed into: the screen most of it is on.</summary>
        Window,

        /// <summary>The pointer, at <see cref="Choice.Point"/>.</summary>
        Pointer,
    }

    /// <summary>Picks the screen for a window that asks about typing in <paramref name="target"/>.</summary>
    public static Choice Choose(IntPtr target) => Choose(target, Sources.System);

    /// <summary>Picks the screen through <paramref name="sources"/>, as above.</summary>
    internal static Choice Choose(IntPtr target, Sources sources)
    {
        if (target == IntPtr.Zero || !sources.WindowExists(target))
        {
            return new Choice(Basis.Pointer, sources.Pointer());
        }

        if (sources.Frame(target) is { } frame && sources.Screen(target) is { } screen && Crosses(frame, screen) && sources.Caret(target) is { } caret)
        {
            return new Choice(Basis.Caret, new Native.POINT { X = caret.Left, Y = caret.Top + ((caret.Bottom - caret.Top) / 2) });
        }

        return new Choice(Basis.Window, default);
    }

    /// <summary>Gets whether <paramref name="frame"/> reaches past an edge of <paramref name="screen"/>, onto another screen.</summary>
    internal static bool Crosses(Native.RECT frame, Native.RECT screen)
        => frame.Left < screen.Left || frame.Top < screen.Top || frame.Right > screen.Right || frame.Bottom > screen.Bottom;

    // The visible frame, without the invisible resize border, which reaches past the screen of a maximized window.
    private static Native.RECT? VisibleFrame(IntPtr window)
    {
        if (Native.DwmGetWindowAttribute(window, Native.DWMWA_EXTENDED_FRAME_BOUNDS, out Native.RECT frame, Marshal.SizeOf<Native.RECT>()) >= 0 && frame.Right > frame.Left && frame.Bottom > frame.Top)
        {
            return frame;
        }

        return Native.GetWindowRect(window, out Native.RECT bounds) ? bounds : null;
    }

    // The whole screen most of the window is on, the taskbar's part included.
    private static Native.RECT? WindowScreen(IntPtr window)
    {
        var info = new Native.MONITORINFO { CbSize = (uint)Marshal.SizeOf<Native.MONITORINFO>() };
        return Native.GetMonitorInfoW(Native.MonitorFromWindow(window, Native.MONITOR_DEFAULTTONEAREST), ref info) ? info.RcMonitor : null;
    }

    private static Native.POINT PointerPosition() => Native.GetCursorPos(out Native.POINT pointer) ? pointer : default;

    /// <summary>A screen picked by <see cref="Basis"/>, with the point that picks it for the caret and the pointer.</summary>
    internal readonly record struct Choice(Basis Basis, Native.POINT Point);

    /// <summary>Where each step looks, so tests can stand in for Windows.</summary>
    internal sealed record Sources(
        Func<IntPtr, bool> WindowExists,
        Func<IntPtr, Native.RECT?> Frame,
        Func<IntPtr, Native.RECT?> Screen,
        Func<IntPtr, Native.RECT?> Caret,
        Func<Native.POINT> Pointer)
    {
        public static Sources System { get; } = new(
            Native.IsWindow,
            VisibleFrame,
            WindowScreen,
            CaretLocator.Find,
            PointerPosition);
    }
}
