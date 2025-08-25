// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using ManagedCommon;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Core;
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
    private const int ErrorReportExitCode = 100;
    private readonly Options _options;

    public ErrorReportWindow(Options options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;

        InitializeComponent();

        SetWindowVisualsSafe();

        this.SetIcon();

        InitializeUI(options);

        CenterAndResizeSafe();

        if (_options.DisableCloseButton || _options.Mode == TroubleMode.Fatal)
        {
            DisableCloseButtonSafe();
        }
    }

    private void SetWindowVisualsSafe()
    {
        try
        {
            var appName = ResourceLoaderInstance.ResourceLoader.GetString("AppName");
            var title = ResourceLoaderInstance.ResourceLoader.GetString("ErrorReportWindow_Title");
            Title = $"{appName} {title}";
        }
        catch (Exception ex)
        {
            Logger.LogDebug($"Failed to set window caption: {ex}");
        }

        try
        {
            AppWindow.TitleBar.PreferredTheme = TitleBarTheme.UseDefaultAppMode;
            AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        }
        catch (Exception ex)
        {
            // If setting the title bar theme fails, we can't do much about it.
            Logger.LogDebug($"Failed to set preferred theme: {ex}");
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
            Logger.LogDebug($"Failed to set presenter: {ex}");
        }

        try
        {
            SystemBackdrop = new MicaBackdrop();
            ExtendsContentIntoTitleBar = true;
        }
        catch (Exception ex)
        {
            Logger.LogDebug($"Failed to set backdrop: {ex}");

            try
            {
                RootGrid.Background = App.Current.Resources["SolidBackgroundFillColorBaseBrush"] as SolidColorBrush;
            }
            catch
            {
                // If setting the background fails, we can't do much about it.
                Logger.LogDebug($"Failed to get brush: {ex}");
            }
        }
    }

    private void InitializeUI(Options options)
    {
        SummaryTextBlock.Text = string.IsNullOrWhiteSpace(options.Summary)
            ? options.Mode == TroubleMode.Recoverable
                ? ResourceLoaderInstance.ResourceLoader.GetString("ErrorReportWindow_Summary_Recoverable")
                : ResourceLoaderInstance.ResourceLoader.GetString("ErrorReportWindow_Summary_Unrecoverable")
            : options.Summary;
        DetailsBox.Text = options.ErrorReport ?? string.Empty;
        ContinueBtn.Visibility = options.Mode == TroubleMode.Recoverable ? Visibility.Visible : Visibility.Collapsed;
        RevealBtn.Visibility = !string.IsNullOrWhiteSpace(options.ReportFilePath) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void CenterAndResizeSafe()
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

    private void CopyDetailsToClipboardSafe()
    {
        try
        {
            ClipboardHelper.SetText(DetailsBox.Text);
        }
        catch (Exception ex)
        {
            // If clipboard access fails, we can't do much about it.
            Logger.LogDebug($"Failed to copy to clipboard: {ex}");
        }
    }

    private void RestartApp()
    {
        var failureReason = AppInstance.Restart(string.Empty);

        /*
         * If the AppInstance.Restart is successful, the app will exit, and we won't reach this point.
         * Following code executes only if the restart failed:
         */

        if (failureReason == AppRestartFailureReason.RestartPending)
        {
            return;
        }

        Logger.LogWarning("Restart failed: " + failureReason);

        // use native message box, since we can't be sure we're not in a bad state
        var hwnd = new HWND(WindowNative.GetWindowHandle(this));
        var messageBody = ResourceLoaderInstance.GetString("ErrorReportWindow_RestartFailedMessageBox_Body");
        var messageTitle = ResourceLoaderInstance.GetString("ErrorReportWindow_RestartFailedMessageBox_Title");
        PInvoke.MessageBox(hwnd, messageBody, messageTitle, MESSAGEBOX_STYLE.MB_OK | MESSAGEBOX_STYLE.MB_ICONERROR);
        ExitApp();
    }

    private static void ExitApp()
    {
        Environment.Exit(ErrorReportExitCode);
    }

    private static void RevealFileSafe(string? path)
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
        catch (Exception ex)
        {
            // If we can't reveal the file or its directory, we can't do much about it.
            // The user can still close the dialog or try again.
            // We don't want to crash the app here.
            Logger.LogDebug($"Failed to reveal file: {ex}");
        }
    }

    private void DisableCloseButtonSafe()
    {
        var hwnd = new HWND(WindowNative.GetWindowHandle(this));
        if (hwnd == default)
        {
            return;
        }

        var hMenu = PInvoke.GetSystemMenu(hwnd, false);
        if (hMenu == default)
        {
            return;
        }

        PInvoke.RemoveMenu(hMenu, PInvoke.SC_CLOSE, MENU_ITEM_FLAGS.MF_BYCOMMAND);
        PInvoke.DrawMenuBar(hwnd);
    }

    private static TextWrapping BoolToWrapping(bool? wordWrap)
    {
        return wordWrap == true ? TextWrapping.Wrap : TextWrapping.NoWrap;
    }

    private void ErrorReportWindow_OnClosed(object sender, WindowEventArgs args)
    {
        // If the window is closed, we can exit or recover based on the mode
        // Since WinUI title bar won't allow to disable close button, let's handle it here
        // as a sensible default behavior.
        if (_options.Mode == TroubleMode.Fatal)
        {
            ExitApp();
        }
    }

    private void CopyBtn_Click(object sender, RoutedEventArgs e)
    {
        CopyDetailsToClipboardSafe();
    }

    private void RevealBtn_Click(object sender, RoutedEventArgs e)
    {
        RevealFileSafe(_options.ReportFilePath);
    }

    private void ContinueBtn_Click(object sender, RoutedEventArgs e)
    {
        Debug.Assert(_options.Mode == TroubleMode.Recoverable, "Continue button shouldn't be reachable in non-recoverable mode");
        Close();
    }

    private void RestartBtn_Click(object sender, RoutedEventArgs e)
    {
        RestartApp();
    }

    private void ExitBtn_Click(object sender, RoutedEventArgs e)
    {
        ExitApp();
    }

    internal sealed class Options
    {
        public string? ErrorReport { get; init; }

        public string? ReportFilePath { get; init; }

        public TroubleMode Mode { get; init; } = TroubleMode.Fatal;

        public bool DisableCloseButton { get; init; }

        public string? Summary { get; init; }
    }

    internal enum TroubleMode
    {
        Recoverable,
        Fatal,
    }
}
