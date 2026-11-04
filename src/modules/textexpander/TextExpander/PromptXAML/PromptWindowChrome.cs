// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics;

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// What the engine's windows, for a question and for a choice, share: Text Expander's icon and name in a title bar that
/// follows the theme, a dialog's frame above the app being typed into and out of the taskbar and Alt+Tab, and a size
/// and place on that app's screen. Used on the prompt thread only.
/// </summary>
internal sealed class PromptWindowChrome
{
    private readonly Window _window;
    private readonly IntPtr _target;
    private RectInt32 _workArea;

    /// <param name="target">The window being typed into. The window opens on its screen.</param>
    public PromptWindowChrome(Window window, IntPtr target, UIElement titleBar, Image titleBarIcon, TextBlock titleText)
    {
        _window = window;
        _target = target;
        Handle = WinRT.Interop.WindowNative.GetWindowHandle(window);

        window.Title = PromptText.Title;
        titleText.Text = PromptText.Title;
        string icon = Path.Combine(AppContext.BaseDirectory, "app.ico");
        if (File.Exists(icon))
        {
            titleBarIcon.Source = new BitmapImage(new Uri(icon));
            window.AppWindow.SetIcon(icon);
        }

        window.ExtendsContentIntoTitleBar = true;
        window.SetTitleBar(titleBar);
        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            // The close button follows the theme too, as in PowerToys' other utilities (TitleBarHelper).
            window.AppWindow.TitleBar.PreferredTheme = PromptApp.ThemeOverride switch
            {
                ApplicationTheme.Light => TitleBarTheme.Light,
                ApplicationTheme.Dark => TitleBarTheme.Dark,
                _ => TitleBarTheme.UseDefaultAppMode,
            };
        }

        // A dialog: no resizing, minimizing or maximizing; above the app it types into; and, like the
        // box it replaces, not in the taskbar or Alt+Tab.
        OverlappedPresenter presenter = OverlappedPresenter.CreateForDialog();
        presenter.IsAlwaysOnTop = true;
        window.AppWindow.SetPresenter(presenter);
        window.AppWindow.IsShownInSwitchers = false;
    }

    public IntPtr Handle { get; }

    /// <summary>Shows the window and brings it in front of the app the user was typing in.</summary>
    public void Open()
    {
        _window.Activate();
        WindowFocus.Force(Handle);
    }

    /// <summary>
    /// Sizes the window and centers it, a little above the middle, on the screen of the window being
    /// typed into, or on the pointer's screen if that window has gone.
    /// </summary>
    public void Place(double widthDip, double heightDip)
    {
        IntPtr monitor;
        DisplayArea area;
        if (_target != IntPtr.Zero && Native.IsWindow(_target))
        {
            monitor = Native.MonitorFromWindow(_target, Native.MONITOR_DEFAULTTONEAREST);
            area = DisplayArea.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(_target), DisplayAreaFallback.Nearest);
        }
        else
        {
            Native.GetCursorPos(out Native.POINT pointer);
            monitor = Native.MonitorFromPoint(pointer, Native.MONITOR_DEFAULTTONEAREST);
            area = DisplayArea.GetFromPoint(new PointInt32(pointer.X, pointer.Y), DisplayAreaFallback.Nearest);
        }

        double scale = Native.GetDpiForMonitor(monitor, Native.MDT_EFFECTIVE_DPI, out uint dpi, out _) == 0 && dpi > 0 ? dpi / 96.0 : 1.0;
        _workArea = area.WorkArea;

        // Onto that screen first, so a change of scale on the way doesn't resize it afterwards. Then the
        // outer size from the real frame: the client area includes the title bar, so AppWindow.ResizeClient,
        // which leaves room for a system caption, would make the window that much too tall.
        AppWindow appWindow = _window.AppWindow;
        appWindow.Move(new PointInt32(_workArea.X, _workArea.Y));
        SizeInt32 window = appWindow.Size;
        Native.GetClientRect(Handle, out Native.RECT client);
        appWindow.Resize(new SizeInt32(
            PromptWindowSize.WindowPixels(window.Width, client.Right - client.Left, widthDip, scale, _workArea.Width),
            PromptWindowSize.WindowPixels(window.Height, client.Bottom - client.Top, heightDip, scale, _workArea.Height)));
        Center();
    }

    /// <summary>
    /// Sizes the window to its laid-out content: the title bar row, text that wraps, and larger text from
    /// Accessibility settings, at the scale the window actually has.
    /// </summary>
    public void FitToContent(FrameworkElement root)
    {
        XamlRoot xamlRoot = root.XamlRoot;
        double scale = xamlRoot.RasterizationScale;
        root.Measure(new global::Windows.Foundation.Size(xamlRoot.Size.Width, double.PositiveInfinity));

        SizeInt32 window = _window.AppWindow.Size;
        int height = PromptWindowSize.WindowPixels(window.Height, (int)Math.Round(xamlRoot.Size.Height * scale), root.DesiredSize.Height, scale, _workArea.Height);
        if (height != window.Height)
        {
            _window.AppWindow.Resize(new SizeInt32(window.Width, height));
            Center();
        }
    }

    /// <summary>Centers the window across, and a third of the way down, the screen's work area.</summary>
    private void Center()
    {
        SizeInt32 size = _window.AppWindow.Size;
        _window.AppWindow.Move(new PointInt32(_workArea.X + ((_workArea.Width - size.Width) / 2), _workArea.Y + ((_workArea.Height - size.Height) / 3)));
    }
}
