// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.ApplicationModel.Resources;
using MonitorPower;
using Windows.Graphics;
using Windows.System;
using WinRT.Interop;

namespace MonitorPower.Runtime;

public sealed partial class MainWindow : Window
{
    private const ushort ControllerAButton = 0x1000;
    private const ushort ControllerBButton = 0x2000;
    private const ushort ControllerDpadUp = 0x0001;
    private const ushort ControllerDpadDown = 0x0002;
    private static readonly ResourceLoader Resources = new("PowerToys.MonitorPower.Runtime.pri");
    private const int OverlayWidthDip = 480;
    private const int OverlayHeightDip = 420;
    private string _topologyAtOpen = string.Empty;
    private bool _isApplying;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaBorderColor = 34;
    private const int DwmwcpRound = 2;
    private const int DwmwaColorNone = unchecked((int)0xFFFFFFFE);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, [MarshalAs(UnmanagedType.Bool)] bool attach);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int index, IntPtr value);

    private const int GwlExStyle = -20;
    private const long WsExTopmost = 0x00000008;
    private const long WsExToolWindow = 0x00000080;
    private static readonly IntPtr HwndTopmost = new(-1);
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;

    public MainWindow()
    {
        var startupTimer = Stopwatch.StartNew();
        RuntimeLog.Info("Profile selector window initialization started.");
        InitializeComponent();
        ConfigureOverlay();
        var profileCount = InitializeSelectorState();
        AppWindow.Closing += AppWindow_Closing;
        Activated += MainWindow_Activated;
        RuntimeLog.Info($"Profile selector initialized in {startupTimer.ElapsedMilliseconds} ms with {profileCount} selectable profile(s).");
    }

    private void ConfigureOverlay()
    {
        // Overlay: no title bar or border, always on top, semi-transparent acrylic background.
        var presenter = OverlappedPresenter.Create();
        presenter.SetBorderAndTitleBar(false, false);
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.IsAlwaysOnTop = true;
        AppWindow.SetPresenter(presenter);
        AppWindow.IsShownInSwitchers = false;
        SystemBackdrop = new DesktopAcrylicBackdrop();

        // Windows 11 look: rounded corners and no white system frame (the XAML border draws a subtle outline).
        var hwnd = WindowNative.GetWindowHandle(this);
        var corner = DwmwcpRound;
        var cornerResult = DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref corner, sizeof(int));
        var borderColor = DwmwaColorNone;
        var borderResult = DwmSetWindowAttribute(hwnd, DwmwaBorderColor, ref borderColor, sizeof(int));
        if (cornerResult != 0 || borderResult != 0)
        {
            RuntimeLog.Warning($"Could not apply Windows 11 window styling (corner HRESULT 0x{cornerResult:X8}, border HRESULT 0x{borderResult:X8}).");
        }
    }

    /// <summary>
    /// Shows the selector centered on the monitor that currently holds the cursor.
    /// Called on every activation: an already existing window may be hidden or behind other windows.
    /// </summary>
    public void ShowOverlay()
    {
        var hwnd = WindowNative.GetWindowHandle(this);

        // Remember the active window before showing anything: the overlay appears on its monitor.
        var targetWindow = GetForegroundWindow();
        InitializeSelectorState();
        CenterOnMonitorOf(targetWindow == hwnd ? IntPtr.Zero : targetWindow);

        // Tool window + topmost keeps it above other windows, including borderless full-screen games.
        var exStyle = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
        SetWindowLongPtr(hwnd, GwlExStyle, new IntPtr(exStyle | WsExTopmost | WsExToolWindow));
        AppWindow.Show();
        SetWindowPos(hwnd, HwndTopmost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate | SwpShowWindow);
        Activate();
        ForceForeground(hwnd);
        SetWindowPos(hwnd, HwndTopmost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate | SwpShowWindow);
        ProfileList.Focus(FocusState.Programmatic);
        RuntimeLog.Info($"Profile selector shown at {AppWindow.Position.X},{AppWindow.Position.Y} size {AppWindow.Size.Width}x{AppWindow.Size.Height}.");
    }

    private static void ForceForeground(IntPtr hwnd)
    {
        // A background process cannot normally take the foreground; briefly sharing input with the current
        // foreground thread lets the overlay receive focus (and keyboard input) over a game.
        var foreground = GetForegroundWindow();
        var foregroundThread = foreground == IntPtr.Zero ? 0 : GetWindowThreadProcessId(foreground, out _);
        var currentThread = GetCurrentThreadId();
        var attached = foregroundThread != 0 && foregroundThread != currentThread &&
            AttachThreadInput(currentThread, foregroundThread, true);
        try
        {
            SetForegroundWindow(hwnd);
        }
        finally
        {
            if (attached)
            {
                AttachThreadInput(currentThread, foregroundThread, false);
            }
        }
    }

    private void CenterOnMonitorOf(IntPtr activeWindow)
    {
        DisplayArea displayArea;
        try
        {
            displayArea = activeWindow != IntPtr.Zero
                ? DisplayArea.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(activeWindow), DisplayAreaFallback.Nearest)
                : GetDisplayAreaFromCursor();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            RuntimeLog.Warning($"Could not resolve the active window monitor ({ex.Message}); using the cursor monitor.");
            displayArea = GetDisplayAreaFromCursor();
        }

        var bounds = displayArea.OuterBounds;

        // Move onto the target monitor first so the window picks up that monitor's DPI, then size it in DIPs.
        AppWindow.MoveAndResize(new RectInt32(bounds.X + (bounds.Width / 2), bounds.Y + (bounds.Height / 2), 1, 1));
        var dpi = GetDpiForWindow(WindowNative.GetWindowHandle(this));
        var scale = dpi > 0 ? dpi / 96.0 : 1.0;
        var width = (int)Math.Round(OverlayWidthDip * scale);
        var height = (int)Math.Round(OverlayHeightDip * scale);
        AppWindow.MoveAndResize(new RectInt32(
            bounds.X + ((bounds.Width - width) / 2),
            bounds.Y + ((bounds.Height - height) / 2),
            width,
            height));
    }

    private static DisplayArea GetDisplayAreaFromCursor()
    {
        var cursor = GetCursorPos(out var point) ? new PointInt32(point.X, point.Y) : new PointInt32(0, 0);
        return DisplayArea.GetFromPoint(cursor, DisplayAreaFallback.Nearest);
    }

    /// <summary>
    /// Hides the overlay without closing the window. Closing the last window would end the whole runtime
    /// process, and the global shortcut would stop working until Settings restarts it.
    /// </summary>
    private void HideOverlay()
    {
        AppWindow.Hide();
        RuntimeLog.Info("Profile selector dismissed and hidden; the global Monitor Power runtime remains active.");
    }

    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        // Click outside the overlay dismisses it, unless a profile is being applied.
        if (args.WindowActivationState == WindowActivationState.Deactivated && !_isApplying && AppWindow.IsVisible)
        {
            AppWindow.Hide();
            RuntimeLog.Info("Profile selector hidden because it lost focus.");
        }
    }

    private int InitializeSelectorState()
    {
        var topologyTimer = Stopwatch.StartNew();
        _topologyAtOpen = GetActiveTopologySignature();
        RuntimeLog.Info($"Selector topology snapshot captured in {topologyTimer.ElapsedMilliseconds} ms.");

        var savedProfiles = DisplayHelpers.GetSavedProfiles();
        var profiles = new List<RuntimeProfile>
        {
            new(string.Empty, GetResourceString("MonitorPower_Selector_AllDisplays"), BuiltInDisplayProfile.AllDisplays),
            new(string.Empty, GetResourceString("MonitorPower_Selector_PrimaryDisplayOnly"), BuiltInDisplayProfile.PrimaryDisplayOnly),
        };
        profiles.AddRange(savedProfiles.Select(profile => new RuntimeProfile(profile.FileName, profile.Name)));
        ProfileList.ItemsSource = profiles;
        ProfileList.SelectedIndex = profiles.Count == 0 ? -1 : 0;
        ProfileList.Focus(FocusState.Programmatic);
        ApplyButton.IsEnabled = profiles.Count > 0;
        SetStatus(savedProfiles.Count == 0 ? GetResourceString("MonitorPower_Selector_NoSavedProfiles") : string.Empty);
        return profiles.Count;
    }

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        args.Cancel = true;
        AppWindow.Hide();
        RuntimeLog.Info("Profile selector dismissed and hidden; the global Monitor Power runtime remains active.");
    }

    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        await ApplySelectedProfileAsync();
    }

    private void ProfileList_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            HideOverlay();
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Enter)
        {
            _ = ApplySelectedProfileAsync();
            e.Handled = true;
        }
    }

    public void HandleControllerButtons(ushort buttons)
    {
        if (!AppWindow.IsVisible)
        {
            return;
        }

        if ((buttons & ControllerBButton) != 0)
        {
            HideOverlay();
        }
        else if ((buttons & ControllerAButton) != 0)
        {
            _ = ApplySelectedProfileAsync();
        }
        else if ((buttons & ControllerDpadUp) != 0 && ProfileList.Items.Count > 0)
        {
            ProfileList.SelectedIndex = Math.Max(0, ProfileList.SelectedIndex - 1);
        }
        else if ((buttons & ControllerDpadDown) != 0 && ProfileList.Items.Count > 0)
        {
            ProfileList.SelectedIndex = Math.Min(ProfileList.Items.Count - 1, ProfileList.SelectedIndex + 1);
        }
    }

    public void SetStatus(string message)
    {
        StatusText.Text = message;
        StatusText.Visibility = string.IsNullOrEmpty(message) ? Visibility.Collapsed : Visibility.Visible;
    }

    private async Task ApplySelectedProfileAsync()
    {
        if (_isApplying)
        {
            return;
        }

        if (ProfileList.SelectedItem is not RuntimeProfile profile)
        {
            RuntimeLog.Warning("Profile apply requested without a selected profile.");
            SetStatus(GetResourceString("MonitorPower_Selector_Error_NoProfileSelected"));
            return;
        }

        var applyTimer = Stopwatch.StartNew();
        RuntimeLog.Info("Profile apply started.");
        _isApplying = true;
        ApplyButton.IsEnabled = false;
        SetStatus(FormatResourceString("MonitorPower_Selector_Applying", profile.Name));
        try
        {
            var result = await Task.Run(() =>
            {
                if (!string.Equals(_topologyAtOpen, GetActiveTopologySignature(), StringComparison.Ordinal))
                {
                    RuntimeLog.Warning("Profile apply canceled because the active display topology changed.");
                    return null;
                }

                return profile.BuiltInProfile switch
                {
                    BuiltInDisplayProfile.AllDisplays => DisplayHelpers.SetAllDisplays(),
                    BuiltInDisplayProfile.PrimaryDisplayOnly => DisplayHelpers.SetPrimaryDisplayOnly(
                        message => DispatcherQueue.TryEnqueue(() => SetStatus(message))),
                    _ => DisplayHelpers.ApplyNamedProfile(
                        profile.FileName,
                        message => DispatcherQueue.TryEnqueue(() => SetStatus(message))),
                };
            });
            if (result is null)
            {
                SetStatus(GetResourceString("MonitorPower_Selector_Error_TopologyChanged"));
                return;
            }

            SetStatus(result);
            if (DisplayHelpers.IsErrorResult(result))
            {
                RuntimeLog.Warning($"Profile apply returned an error after {applyTimer.ElapsedMilliseconds} ms: {result}");
                ApplyButton.IsEnabled = true;
            }
            else
            {
                RuntimeLog.Info($"Profile apply completed in {applyTimer.ElapsedMilliseconds} ms.");
            }
        }
        catch (Exception ex)
        {
            RuntimeLog.Error($"Profile apply threw after {applyTimer.ElapsedMilliseconds} ms.", ex);
            SetStatus(FormatResourceString("MonitorPower_Selector_Error_ApplyFailed", profile.Name, ex.Message));
            ApplyButton.IsEnabled = true;
        }
        finally
        {
            _isApplying = false;
        }
    }

    private static string GetActiveTopologySignature()
    {
        var (paths, modes) = DisplayHelpers.GetActivePaths();
        return string.Join(
            "|",
            paths.Select(path =>
            {
                var modeIndex = path.sourceInfo.sourceModeInfoIdx;
                var mode = modeIndex < modes.Length ? modes[modeIndex] : default;
                var geometry = mode.infoType == DISPLAYCONFIG_MODE_INFO_TYPE.Source
                    ? $"{mode.modeInfo.sourceMode.width}x{mode.modeInfo.sourceMode.height}@{mode.modeInfo.sourceMode.position.x},{mode.modeInfo.sourceMode.position.y}"
                    : "unknown";
                return $"{path.targetInfo.adapterId}:{path.targetInfo.id}:{path.targetInfo.rotation}:{geometry}";
            })
            .Distinct(StringComparer.Ordinal)
            .OrderBy(target => target, StringComparer.Ordinal));
    }

    internal static string GetProfileDescription(BuiltInDisplayProfile profile)
        => GetResourceString(profile == BuiltInDisplayProfile.None
            ? "MonitorPower_Selector_Profile_Saved"
            : "MonitorPower_Selector_Profile_BuiltIn");

    private static string GetResourceString(string key)
        => Resources.GetString(key);

    private static string FormatResourceString(string key, params object[] arguments)
        => string.Format(System.Globalization.CultureInfo.CurrentCulture, GetResourceString(key), arguments);
}
