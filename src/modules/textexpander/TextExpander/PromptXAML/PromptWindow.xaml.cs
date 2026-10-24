// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics;
using Windows.System;

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// The window a {{prompt:Label}} snippet opens: the question over a box, then OK and Cancel. It
/// follows Windows' theme, high contrast included, and is sharp at every scale. Shown by
/// <see cref="PromptHost"/> on the prompt thread, never on the keyboard hook's.
/// </summary>
public sealed partial class PromptWindow : Window
{
    private const double WidthDip = 420;

    // What a one-line question lays out at, title bar included, so the window opens at its size.
    // Refit once it's loaded, for a question that wraps or larger text.
    private const double EstimatedHeightDip = 209.6;

    private readonly IntPtr _target;
    private readonly IntPtr _hwnd;
    private readonly Action<string?> _done;
    private readonly PromptRequest _request;
    private RectInt32 _workArea;
    private bool _finished;

    /// <param name="label">The snippet's question.</param>
    /// <param name="target">The window being typed into. The prompt opens on its screen.</param>
    /// <param name="done">Called once, on this thread, with the answer, or with null when cancelled.</param>
    internal PromptWindow(PromptRequest request, IntPtr target, Action<string?> done)
    {
        _request = request;
        _target = target;
        _done = done;
        InitializeComponent();
        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

        Title = PromptText.Title;
        TitleText.Text = PromptText.Title;
        Answer.Header = PromptText.Label(request.Label);
        AnswerError.Text = PromptText.RequiredError;
        OkButton.Content = PromptText.OkText;
        CancelButton.Content = PromptText.CancelText;
        OkButton.IsEnabled = !request.Required;

        string icon = Path.Combine(AppContext.BaseDirectory, "app.ico");
        if (File.Exists(icon))
        {
            TitleBarIcon.Source = new BitmapImage(new Uri(icon));
            AppWindow.SetIcon(icon);
        }

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBar);
        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            // The close button follows the theme too, as in PowerToys' other utilities (TitleBarHelper).
            AppWindow.TitleBar.PreferredTheme = PromptApp.ThemeOverride switch
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
        AppWindow.SetPresenter(presenter);
        AppWindow.IsShownInSwitchers = false;

        // The close button and Alt+F4 cancel, as Esc does.
        AppWindow.Closing += (_, _) => Finish(accepted: false, close: false);
        Closed += (_, _) => Complete(null);

        Place(EstimatedHeightDip);
    }

    /// <summary>Shows the window and brings it in front of the app the user was typing in.</summary>
    internal void Open()
    {
        Activate();
        WindowFocus.Force(_hwnd);
    }

    private void Root_Loaded(object sender, RoutedEventArgs e)
    {
        FitToContent();
        Answer.Focus(FocusState.Programmatic);
    }

    /// <summary>
    /// Sizes the window to its laid-out content: the title bar row, a question that wraps, and larger
    /// text from Accessibility settings, at the scale the window actually has.
    /// </summary>
    private void FitToContent()
    {
        XamlRoot xamlRoot = Root.XamlRoot;
        double scale = xamlRoot.RasterizationScale;
        Root.Measure(new global::Windows.Foundation.Size(xamlRoot.Size.Width, double.PositiveInfinity));

        SizeInt32 window = AppWindow.Size;
        int height = PromptWindowSize.WindowPixels(window.Height, (int)Math.Round(xamlRoot.Size.Height * scale), Root.DesiredSize.Height, scale, _workArea.Height);
        if (height != window.Height)
        {
            AppWindow.Resize(new SizeInt32(window.Width, height));
            Center();
        }
    }

    private void Root_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            // OK is the default. A focused button handles Enter itself, so this is everywhere else.
            case VirtualKey.Enter:
                e.Handled = true;
                TryFinish();
                break;

            case VirtualKey.Escape:
                e.Handled = true;
                Finish(accepted: false, close: true);
                break;
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => TryFinish();

    private void Cancel_Click(object sender, RoutedEventArgs e) => Finish(accepted: false, close: true);

    private void Answer_TextChanged(object sender, TextChangedEventArgs e)
    {
        bool valid = PromptText.HasRequiredAnswer(_request, Answer.Text);
        OkButton.IsEnabled = valid;
        if (valid)
        {
            AnswerError.Visibility = Visibility.Collapsed;
        }
    }

    private void TryFinish()
    {
        if (!PromptText.HasRequiredAnswer(_request, Answer.Text))
        {
            AnswerError.Visibility = Visibility.Visible;
            Answer.Focus(FocusState.Programmatic);
            return;
        }

        Finish(accepted: true, close: true);
    }

    private void Finish(bool accepted, bool close)
    {
        if (_finished)
        {
            return;
        }

        string typed = PromptText.Take(() => Answer.Text, () => Answer.Text = string.Empty);
        Complete(PromptText.Result(accepted, typed));
        if (close)
        {
            Close();
        }
    }

    private void Complete(string? answer)
    {
        if (_finished)
        {
            return;
        }

        _finished = true;
        _done(answer);
    }

    /// <summary>
    /// Sizes the window and centers it, a little above the middle, on the screen of the window being
    /// typed into, or on the pointer's screen if that window has gone.
    /// </summary>
    private void Place(double heightDip)
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
        AppWindow.Move(new PointInt32(_workArea.X, _workArea.Y));
        SizeInt32 window = AppWindow.Size;
        Native.GetClientRect(_hwnd, out Native.RECT client);
        AppWindow.Resize(new SizeInt32(
            PromptWindowSize.WindowPixels(window.Width, client.Right - client.Left, WidthDip, scale, _workArea.Width),
            PromptWindowSize.WindowPixels(window.Height, client.Bottom - client.Top, heightDip, scale, _workArea.Height)));
        Center();
    }

    /// <summary>Centers the window across, and a third of the way down, the screen's work area.</summary>
    private void Center()
    {
        SizeInt32 size = AppWindow.Size;
        AppWindow.Move(new PointInt32(_workArea.X + ((_workArea.Width - size.Width) / 2), _workArea.Y + ((_workArea.Height - size.Height) / 3)));
    }
}
