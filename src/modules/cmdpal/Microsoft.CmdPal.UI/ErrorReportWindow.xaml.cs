// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using ManagedCommon;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using WinRT.Interop;

namespace Microsoft.CmdPal.UI;

/// <summary>
/// Error report window for Command Palette.
/// </summary>
/// <remarks>
/// This window should be reusable as a standalone window, but if combined with situation where there is risk of showing this in a cycle,
/// <see cref="ErrorReportWindowManager"/> should be used.
/// </remarks>
internal sealed partial class ErrorReportWindow
{
    private readonly Options _options;

    public ErrorReportWindow(Options options)
    {
        InitializeComponent();

        TrySetWindowVisuals();

        this.SetIcon();

        _options = options;

        SummaryText.Text = string.IsNullOrWhiteSpace(_options.Summary)
            ? _options.Mode == TroubleMode.Recoverable ? "A recoverable error occurred" : "An unrecoverable error occurred"
            : _options.Summary;

        DetailsBox.Text = options.ErrorReport;

        ContinueBtn.Visibility = _options.Mode == TroubleMode.Recoverable ? Visibility.Visible : Visibility.Collapsed;
        RetryBtn.Visibility = _options.Mode == TroubleMode.Recoverable && _options.RetryAction != null
            ? Visibility.Visible
            : Visibility.Collapsed;

        RevealBtn.Visibility = !string.IsNullOrWhiteSpace(_options.ReportFilePath) ? Visibility.Visible : Visibility.Collapsed;
        if (_options.RetryAction is not null && !string.IsNullOrWhiteSpace(_options.RetryLabel))
        {
            RetryBtn.Content = _options.RetryLabel;
        }

        TryPosition();

        if (_options.DisableCloseButton || _options.Mode == TroubleMode.Fatal)
        {
            TryDisableCloseButton();
        }
    }

    private void ErrorReportWindow_OnClosed(object sender, WindowEventArgs args)
    {
        // If the window is closed, we can exit or recover based on the mode
        // Since WinUI title bar won't allow to disable close button, let's handle it here
        // as a sensible default behavior.
        if (_options.Mode == TroubleMode.Fatal)
        {
            Environment.Exit(-1);
        }
    }

    private void TrySetWindowVisuals()
    {
        try
        {
            AppWindow.TitleBar.PreferredTheme = TitleBarTheme.UseDefaultAppMode;
        }
        catch
        {
            // If setting the title bar theme fails, we can't do much about it.
        }

        try
        {
            var presenter = OverlappedPresenter.Create();

            // We can't make it the window modal, since we would have to make it owned by the main window,
            // and that would make it cloaked when the main window is.
            presenter.IsModal = false; // can't be modal; see warning above
            presenter.IsAlwaysOnTop = true;
            presenter.IsMaximizable = true;
            presenter.IsMinimizable = false;
            presenter.IsResizable = true;

            presenter.PreferredMinimumWidth = 740;
            presenter.PreferredMinimumHeight = 540;

            presenter.SetBorderAndTitleBar(true, true);
            AppWindow.SetPresenter(presenter);
        }
        catch (Exception ex)
        {
            // If setting the presenter fails, we can't do much about it.
            // The user can still close the dialog or try again.
            // We don't want to crash the app here.
            Logger.LogDebug($"Failed to set presenter: {ex.Message}");
        }

        try
        {
            SystemBackdrop = new MicaBackdrop();
            ExtendsContentIntoTitleBar = true;
        }
        catch (Exception ex)
        {
            Logger.LogDebug("Failed to set backdrop:\n" + ex);

            try
            {
                RootGrid.Background = App.Current.Resources["SolidBackgroundFillColorBaseBrush"] as SolidColorBrush;
            }
            catch
            {
                // If setting the background fails, we can't do much about it.
            }
        }
    }

    private void WrapToggle_Checked(object sender, RoutedEventArgs e)
    {
        DetailsBox.TextWrapping = WrapToggle.IsChecked == true ? TextWrapping.Wrap : TextWrapping.NoWrap;
    }

    private void CopyBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dp = new DataPackage();
            dp.SetText(DetailsBox.Text);
            Clipboard.SetContent(dp);
        }
        catch
        {
            // If clipboard access fails, we can't do much about it.
        }
    }

    private void RevealBtn_Click(object sender, RoutedEventArgs e)
    {
        TryRevealFile(_options.ReportFilePath);
    }

    private void ContinueBtn_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void RetryBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _options.RetryAction?.Invoke();
        }
        catch
        {
            // If the retry action fails, we can't do much about it.
            // The user can still close the dialog or try again.
            // We don't want to crash the app here.
        }
        finally
        {
            Close();
        }
    }

    private void RestartBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(Environment.ProcessPath!)
            {
                WorkingDirectory = Environment.CurrentDirectory,
                UseShellExecute = true,
            });
        }
        catch
        {
            // If the restart fails, we can't do much about it.
            // The user can still close the dialog or try again.
            // We don't want to crash the app here.
        }
        finally
        {
            Environment.Exit(-1);
        }
    }

    private void QuitBtn_Click(object sender, RoutedEventArgs e)
    {
        Environment.Exit(-1);
    }

    private void TryPosition()
    {
        var displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest);

        // change size ...
        var work = displayArea.WorkArea;
        var workW = Math.Max(0, work.Width);
        var workH = Math.Max(0, work.Height);

        const int minW = 740, maxW = 1600;
        const int minH = 540, maxH = 1000;

        var desiredW = Math.Clamp((int)(workW * .5), minW, Math.Min(maxW, workW));
        var desiredH = Math.Clamp((int)(workH * .5), minH, Math.Min(maxH, workH));

        AppWindow.Resize(new SizeInt32(desiredW, desiredH));

        // ... and position
        MonitorHelper.PositionCentered(AppWindow);
    }

    private static void TryRevealFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe")
            {
                UseShellExecute = true,
                Arguments = $"/select,\"{path}\"",
            });
        }
        catch
        {
            // If we can't reveal the file or its directory, we can't do much about it.
            // The user can still close the dialog or try again.
            // We don't want to crash the app here.
        }
    }

    private void TryDisableCloseButton()
    {
        var hwnd = new HWND(WindowNative.GetWindowHandle(this));
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var hMenu = PInvoke.GetSystemMenu(hwnd, false);
        if (hMenu == IntPtr.Zero)
        {
            return;
        }

        PInvoke.RemoveMenu(hMenu, PInvoke.SC_CLOSE, MENU_ITEM_FLAGS.MF_BYCOMMAND);
        PInvoke.DrawMenuBar(hwnd);
    }

    internal sealed class Options
    {
        public string? ErrorReport { get; init; }

        public string? ReportFilePath { get; init; }

        public TroubleMode Mode { get; init; } = TroubleMode.Fatal;

        public bool DisableCloseButton { get; init; }

        public string? Summary { get; init; }

        public Action? RetryAction { get; init; }

        public string? RetryLabel { get; init; }
    }

    internal enum TroubleMode
    {
        Recoverable,
        Fatal,
    }
}
