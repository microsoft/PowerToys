// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// A small notice at the text cursor, such as "No email address found", about something an expansion left out. It's a
/// layered tool window that never activates: it has no taskbar button, keys typed while it shows go on into the app,
/// and clicking it only dismisses it. It fades after about 4 seconds, or soon after the user's next key or click, and
/// one shows at a time. Screen readers hear it through a UI Automation notification. It has a thread of its own,
/// started with the first notice, because the keyboard hook's thread must keep answering Windows.
/// </summary>
internal static class NoticeBubble
{
    internal const string WindowClassName = "PowerToys.TextExpander.Notice";

    // Brings the window in line with the latest Show or Dismiss.
    private const uint SyncMessage = Native.WM_APP + 1;

    // A key or click, for the notice whose generation is in wParam.
    private const uint InputMessage = Native.WM_APP + 2;

    // UI Automation's Window pattern closed the notice whose generation is in wParam.
    private const uint CloseMessage = Native.WM_APP + 3;

    private static readonly IntPtr AnimationTimer = new(3);

    private static readonly object StartLock = new();
    private static readonly Native.WndProc WindowProcedure = WindowProc;
    private static readonly NoticeRequests Requests = new();

    private static Thread? _thread;
    private static IntPtr _window;

    // The generation of the notice on the screen, or 0; and the one a key or click was last reported for.
    private static long _shown;
    private static long _inputReported;

    // The rest is only used on the notice's thread.
    private static long _synced;

    // Each notice's own timers: KillTimer leaves a tick already posted, which mustn't fade the notice that replaced it.
    private static IntPtr _lifetimeTimer;
    private static IntPtr _inputTimer;
    private static NoticePainter? _painter;
    private static NoticePainter.Frame? _frame;
    private static NoticeAutomation? _automation;
    private static long _shownAt;
    private static long _fadeStartedAt;
    private static Fade _fade;
    private static byte _opacity;
    private static byte _fadeFrom;
    private static bool _animate;

    private enum Fade
    {
        None,
        In,
        Out,
    }

    /// <summary>
    /// Shows a notice at the caret of <paramref name="targetWindow"/>, in place of any showing. Thread-safe and
    /// non-blocking: it returns at once, and the notice appears on its own thread. It never activates a window or takes
    /// the focus. Never called from the hook callback.
    /// </summary>
    public static void Show(IntPtr targetWindow, string heading, string message)
    {
        var notice = new NoticeRequests.Notice(targetWindow, heading ?? string.Empty, message ?? string.Empty);
        if (string.IsNullOrWhiteSpace(notice.Heading) && string.IsNullOrWhiteSpace(notice.Message))
        {
            return;
        }

        // Either this sees the window, or the notice's thread, once it has made it, sees the request: both sides fence.
        _ = Requests.Set(notice);
        EnsureStarted();
        _ = Post(SyncMessage, 0);
    }

    /// <summary>
    /// Starts to fade a notice that's showing, once it has been up a moment. Cheap and non-blocking, so the keyboard and
    /// mouse hook callbacks can call it for every key and click: it reads a number, and posts one message per notice.
    /// </summary>
    public static void OnUserInput()
    {
        long shown = Volatile.Read(ref _shown);
        if (shown != 0 && Interlocked.Exchange(ref _inputReported, shown) != shown)
        {
            _ = Post(InputMessage, shown);
        }
    }

    /// <summary>Takes down the notice at once, and drops one that hasn't appeared yet.</summary>
    public static void Dismiss()
    {
        _ = Requests.Set(null);
        _ = Post(SyncMessage, 0);
    }

    private static bool Post(uint message, long generation)
    {
        IntPtr window = Volatile.Read(ref _window);
        return window != IntPtr.Zero && Native.PostMessageW(window, message, new IntPtr(generation), IntPtr.Zero);
    }

    private static void EnsureStarted()
    {
        lock (StartLock)
        {
            if (_thread is not null)
            {
                return;
            }

            var thread = new Thread(Run)
            {
                IsBackground = true,
                Name = "PowerToys Text Expander notice",
            };

            // Not single-threaded: a wait there runs other messages, so waiting for UI Automation could reenter the window.
            thread.SetApartmentState(ApartmentState.MTA);
            thread.Start();
            _thread = thread;
        }
    }

    private static void Run()
    {
        try
        {
            // Drawn in physical pixels for the monitor the caret is on.
            _ = Native.SetThreadDpiAwarenessContext(Native.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
            IntPtr window = CreateWindow();
            if (window == IntPtr.Zero)
            {
                Trace.TraceWarning($"Text Expander couldn't create its notice window (error {Marshal.GetLastWin32Error()}), so notices won't show.");
                return;
            }

            try
            {
                _automation = new NoticeAutomation(window, static () => Post(CloseMessage, Volatile.Read(ref _shown)));
            }
            catch (Exception ex)
            {
                // The notice still shows; screen readers just aren't told about it.
                Trace.TraceWarning($"Text Expander's notice has no UI Automation provider: {ex.Message}");
            }

            _ = Interlocked.Exchange(ref _window, window);
            if (Requests.Latest.Generation != 0)
            {
                _ = Post(SyncMessage, 0);
            }

            while (Native.GetMessageW(out Native.MSG msg, IntPtr.Zero, 0, 0) > 0)
            {
                Native.TranslateMessage(ref msg);
                Native.DispatchMessageW(ref msg);
            }
        }
        catch (Exception ex)
        {
            // Without its notices, the engine goes on expanding.
            Trace.TraceWarning($"Text Expander's notice thread stopped: {ex.Message}");
        }
    }

    private static IntPtr CreateWindow()
    {
        IntPtr instance = Native.GetModuleHandleW(null);
        var windowClass = new Native.WNDCLASSEXW
        {
            CbSize = (uint)Marshal.SizeOf<Native.WNDCLASSEXW>(),
            LpfnWndProc = Marshal.GetFunctionPointerForDelegate(WindowProcedure),
            HInstance = instance,
            HCursor = Native.LoadCursorW(IntPtr.Zero, new IntPtr(Native.IDC_ARROW)),
            LpszClassName = WindowClassName,
        };

        if (Native.RegisterClassExW(ref windowClass) == 0 && Marshal.GetLastWin32Error() != Native.ERROR_CLASS_ALREADY_EXISTS)
        {
            return IntPtr.Zero;
        }

        // Layered, for its rounded shape and shadow; a tool window, so it has no taskbar button; never activated, so
        // what's typed while it shows goes on into the app.
        return Native.CreateWindowExW(
            Native.WS_EX_LAYERED | Native.WS_EX_TOOLWINDOW | Native.WS_EX_TOPMOST | Native.WS_EX_NOACTIVATE,
            WindowClassName,
            string.Empty,
            Native.WS_POPUP,
            0,
            0,
            0,
            0,
            IntPtr.Zero,
            IntPtr.Zero,
            instance,
            IntPtr.Zero);
    }

    private static IntPtr WindowProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            switch (msg)
            {
                case SyncMessage:
                    Sync(hwnd);
                    return IntPtr.Zero;

                // Only for the notice on the screen: one reported before it replaced another would fade it too soon.
                case InputMessage:
                    if (wParam.ToInt64() == _shown)
                    {
                        OnInput(hwnd);
                    }

                    return IntPtr.Zero;

                case CloseMessage:
                    if (wParam.ToInt64() == _shown)
                    {
                        Hide(hwnd);
                    }

                    return IntPtr.Zero;

                case Native.WM_TIMER:
                    OnTimer(hwnd, wParam);
                    return IntPtr.Zero;

                case Native.WM_MOUSEACTIVATE:
                    return new IntPtr(Native.MA_NOACTIVATE);

                // Held until the button comes up, so the release doesn't reach the app under the notice.
                case Native.WM_LBUTTONDOWN:
                case Native.WM_RBUTTONDOWN:
                case Native.WM_MBUTTONDOWN:
                    _ = Native.SetCapture(hwnd);
                    return IntPtr.Zero;

                case Native.WM_LBUTTONUP:
                case Native.WM_RBUTTONUP:
                case Native.WM_MBUTTONUP:
                    Hide(hwnd);
                    return IntPtr.Zero;

                case Native.WM_GETOBJECT:
                    if (unchecked((int)(long)lParam) == Native.UiaRootObjectId && _automation is { } automation)
                    {
                        return automation.Answer(wParam, lParam);
                    }

                    break;

                // Each notice is drawn for its monitor's DPI before it's placed there.
                case Native.WM_DPICHANGED:
                    return IntPtr.Zero;

                case Native.WM_DISPLAYCHANGE:
                    Hide(hwnd);
                    break;
            }
        }
        catch (Exception ex)
        {
            // Nothing may escape into Windows from here.
            Trace.TraceWarning($"Text Expander's notice failed: {ex.Message}");
            try
            {
                Hide(hwnd);
            }
            catch (Exception)
            {
            }

            return IntPtr.Zero;
        }

        return Native.DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    /// <summary>
    /// Shows the notice asked for last, or takes the notice down if it was dismissed. Messages can arrive in another
    /// order than the calls that posted them, so each only wakes this up, and the latest request decides. A notice that
    /// was hidden, by a click or a fade, isn't shown again for the same request.
    /// </summary>
    private static void Sync(IntPtr hwnd)
    {
        NoticeRequests.Ask latest = Requests.Latest;
        if (latest.Generation == _synced)
        {
            return;
        }

        if (latest.Notice is not { } notice)
        {
            _synced = latest.Generation;
            Hide(hwnd);
            return;
        }

        Present(hwnd, notice, latest.Generation);
    }

    private static void Present(IntPtr hwnd, NoticeRequests.Notice notice, long generation)
    {
        Native.RECT caret = CaretLocator.Locate(notice.Target);

        // Finding the caret can take a moment. A Show or Dismiss meanwhile overtakes this notice; its message comes next.
        if (Requests.Overtaken(generation))
        {
            return;
        }

        _synced = generation;
        IntPtr monitor = Native.MonitorFromRect(ref caret, Native.MONITOR_DEFAULTTONEAREST);
        var info = new Native.MONITORINFO { CbSize = (uint)Marshal.SizeOf<Native.MONITORINFO>() };
        if (!Native.GetMonitorInfoW(monitor, ref info))
        {
            return;
        }

        uint dpi = Native.GetDpiForMonitor(monitor, Native.MDT_EFFECTIVE_DPI, out uint dpiX, out _) == 0 && dpiX > 0 ? dpiX : 96;
        _painter ??= new NoticePainter();
        NoticePainter.Frame frame = _painter.Paint(notice.Heading, notice.Message, caret, info.RcWork, dpi, TextScale(), NoticeTheme.Current(), CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft);
        if (Requests.Overtaken(generation))
        {
            frame.Dispose();
            return;
        }

        // A notice that replaces one showing appears at once; a new one fades in, unless Windows' animations are off.
        bool replacing = Volatile.Read(ref _shown) != 0;
        _frame?.Dispose();
        _frame = frame;
        _animate = AnimationsEnabled();
        _fade = replacing || !_animate ? Fade.None : Fade.In;
        _opacity = _fade == Fade.In ? (byte)0 : (byte)255;
        _shownAt = _fadeStartedAt = Environment.TickCount64;
        if (_automation is { } automation)
        {
            automation.Name = NoticeAutomation.SpokenText(notice.Heading, notice.Message);
        }

        if (!Update(hwnd))
        {
            Hide(hwnd);
            return;
        }

        _ = Native.SetWindowPos(hwnd, Native.HWND_TOPMOST, 0, 0, 0, 0, Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW);
        Volatile.Write(ref _shown, generation);

        StopTimers(hwnd);
        _lifetimeTimer = new IntPtr((generation << 2) | 1);
        _inputTimer = new IntPtr((generation << 2) | 2);
        _ = Native.SetTimer(hwnd, _lifetimeTimer, (uint)NoticeTiming.Lifetime(MessageDurationSeconds()), IntPtr.Zero);
        if (_fade == Fade.In)
        {
            _ = Native.SetTimer(hwnd, AnimationTimer, NoticeTiming.FrameMs, IntPtr.Zero);
        }
        else
        {
            _ = Native.KillTimer(hwnd, AnimationTimer);
        }

        _automation?.Announce();
    }

    private static void OnInput(IntPtr hwnd)
    {
        if (_fade == Fade.Out)
        {
            return;
        }

        int delay = NoticeTiming.FadeDelay(_shownAt, Environment.TickCount64);
        if (delay == 0)
        {
            FadeOut(hwnd);
        }
        else
        {
            _ = Native.SetTimer(hwnd, _inputTimer, (uint)delay, IntPtr.Zero);
        }
    }

    private static void OnTimer(IntPtr hwnd, IntPtr timer)
    {
        if (timer == AnimationTimer)
        {
            Animate(hwnd);
            return;
        }

        _ = Native.KillTimer(hwnd, timer);
        if (timer == _lifetimeTimer || timer == _inputTimer)
        {
            FadeOut(hwnd);
        }
    }

    private static void FadeOut(IntPtr hwnd)
    {
        if (Volatile.Read(ref _shown) == 0 || _fade == Fade.Out)
        {
            return;
        }

        StopTimers(hwnd);
        if (!_animate)
        {
            Hide(hwnd);
            return;
        }

        _fade = Fade.Out;
        _fadeFrom = _opacity;
        _fadeStartedAt = Environment.TickCount64;
        _ = Native.SetTimer(hwnd, AnimationTimer, NoticeTiming.FrameMs, IntPtr.Zero);
    }

    private static void StopTimers(IntPtr hwnd)
    {
        _ = Native.KillTimer(hwnd, _lifetimeTimer);
        _ = Native.KillTimer(hwnd, _inputTimer);
    }

    private static void Animate(IntPtr hwnd)
    {
        long elapsed = Environment.TickCount64 - _fadeStartedAt;
        switch (_fade)
        {
            case Fade.In:
                _opacity = NoticeTiming.Opacity(elapsed, NoticeTiming.FadeInMs, 0, 255);
                if (elapsed >= NoticeTiming.FadeInMs)
                {
                    _fade = Fade.None;
                    _ = Native.KillTimer(hwnd, AnimationTimer);
                }

                _ = Update(hwnd);
                break;

            case Fade.Out:
                if (elapsed >= NoticeTiming.FadeOutMs)
                {
                    Hide(hwnd);
                    break;
                }

                _opacity = NoticeTiming.Opacity(elapsed, NoticeTiming.FadeOutMs, _fadeFrom, 0);
                _ = Update(hwnd);
                break;

            default:
                _ = Native.KillTimer(hwnd, AnimationTimer);
                break;
        }
    }

    private static bool Update(IntPtr hwnd)
    {
        if (_frame is not { } frame)
        {
            return false;
        }

        Native.POINT position = frame.Position;
        Native.SIZE size = frame.Size;
        Native.POINT source = default;
        var blend = new Native.BLENDFUNCTION
        {
            BlendOp = Native.AC_SRC_OVER,
            SourceConstantAlpha = _opacity,
            AlphaFormat = Native.AC_SRC_ALPHA,
        };
        return Native.UpdateLayeredWindow(hwnd, IntPtr.Zero, ref position, ref size, frame.DC, ref source, 0, ref blend, Native.ULW_ALPHA);
    }

    private static void Hide(IntPtr hwnd)
    {
        StopTimers(hwnd);
        _ = Native.KillTimer(hwnd, AnimationTimer);
        if (Native.GetCapture() == hwnd)
        {
            _ = Native.ReleaseCapture();
        }

        _ = Native.ShowWindow(hwnd, Native.SW_HIDE);
        Volatile.Write(ref _shown, 0);
        _fade = Fade.None;
        _frame?.Dispose();
        _frame = null;
    }

    // Accessibility > Visual effects > Animation effects.
    private static bool AnimationsEnabled()
        => !Native.SystemParametersInfoW(Native.SPI_GETCLIENTAREAANIMATION, 0, out uint enabled, 0) || enabled != 0;

    // Accessibility > Text size, which Windows' own flyouts, and the prompt window, follow.
    private static double TextScale()
    {
        try
        {
            return new global::Windows.UI.ViewManagement.UISettings().TextScaleFactor;
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or TypeLoadException)
        {
            return 1;
        }
    }

    // Accessibility > Visual effects > Dismiss notifications after this amount of time.
    private static uint MessageDurationSeconds()
        => Native.SystemParametersInfoW(Native.SPI_GETMESSAGEDURATION, 0, out uint seconds, 0) ? seconds : 5;
}
