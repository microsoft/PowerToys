// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using ManagedCommon;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.Messages;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.PowerToys.Common.UI.Controls.Window;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using Windows.System;
using Windows.Win32;
using Windows.Win32.Foundation;
using WinUIEx;

namespace Microsoft.CmdPal.UI.Tray;

public sealed partial class TrayPaletteWindow : WindowEx
{
    private readonly IThemeService _themeService;
    private readonly ISettingsService _settingsService;
    private readonly HiddenOwnerWindowBehavior _hiddenOwner = new();
    private RECT _anchor;
    private bool _closed;
    private bool _visible;

    public TrayPaletteViewModel ViewModel { get; }

    public TrayPaletteWindow()
    {
        var services = App.Current.Services;
        _settingsService = services.GetRequiredService<ISettingsService>();
        _themeService = services.GetRequiredService<IThemeService>();
        ViewModel = new(_settingsService, services.GetRequiredService<TopLevelCommandManager>(), services.GetRequiredService<IContextMenuFactory>());
        InitializeComponent();
        Title = ResourceLoaderInstance.GetString("TrayPalette_Title");
        SystemBackdrop = new DesktopAcrylicBackdrop();
        _hiddenOwner.ShowInTaskbar(this, false);
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsResizable = false;
            presenter.IsMinimizable = false;
            presenter.IsMaximizable = false;
        }

        Root.RequestedTheme = _themeService.Current.Theme;
        Root.KeyDown += Root_KeyDown;
        Activated += Window_Activated;
        Closed += Window_Closed;
        _themeService.ThemeChanged += ThemeChanged;
        _settingsService.SettingsChanged += SettingsChanged;
    }

    internal void Toggle(RECT anchor)
    {
        if (_visible)
        {
            Dismiss();
            return;
        }

        _anchor = anchor;
        _visible = true;
        PositionWindow();
        Activate();
        PInvoke.SetForegroundWindow(this.GetWindowHwnd());
        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        try
        {
            await ViewModel.RefreshAsync();
            if (!_closed && _visible)
            {
                PositionWindow();
                CommandsGrid.Focus(FocusState.Programmatic);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to refresh Tray Palette.", ex);
        }
    }

    private void PositionWindow()
    {
        var display = DisplayArea.GetFromPoint(new((_anchor.left + _anchor.right) / 2, (_anchor.top + _anchor.bottom) / 2), DisplayAreaFallback.Nearest);
        var scale = FlyoutWindowHelper.GetDpiScale(display);
        var work = display.WorkArea;
        var rows = Math.Max(1, (ViewModel.Items.Count + 2) / 3);
        var heightDip = Math.Min((rows * 100) + 104, FlyoutWindowHelper.ScaleToDip(work.Height, scale) - 16);
        var width = Math.Min(FlyoutWindowHelper.ScaleToPhysicalPixels(360, scale), work.Width);
        var height = FlyoutWindowHelper.ScaleToPhysicalPixels(heightDip, scale);
        var gap = FlyoutWindowHelper.ScaleToPhysicalPixels(8, scale);
        var x = Math.Clamp(_anchor.right - width, work.X, work.X + work.Width - width);
        var y = _anchor.top >= work.Y + (work.Height / 2)
            ? _anchor.top - height - gap
            : _anchor.bottom + gap;
        y = Math.Clamp(y, work.Y, work.Y + work.Height - height);
        FlyoutWindowHelper.MoveAndResizeOnDisplay(this, display, new RectInt32(x, y, width, height));
    }

    private void CommandsGrid_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not TrayPaletteItemViewModel item)
        {
            return;
        }

        var message = item.CreateMessage();
        message.ShowWindowIfPage = true;
        message.OnBeforeShowConfirmation = ShowPalette;
        Dismiss();
        WeakReferenceMessenger.Default.Send(message);
    }

    private void CommandsGrid_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (!args.InRecycleQueue && args.Item is TrayPaletteItemViewModel item)
        {
            AutomationProperties.SetName(args.ItemContainer, item.Item.Title);
            AutomationProperties.SetAutomationId(args.ItemContainer, $"TrayPalette_{item.Pin.ProviderId}_{item.Pin.CommandId}");
        }
    }

    private void OpenPalette_Click(object sender, RoutedEventArgs e)
    {
        Dismiss();
        ShowPalette();
    }

    private static void ShowPalette() =>
        WeakReferenceMessenger.Default.Send<HotkeySummonMessage>(new(string.Empty, HWND.Null));

    private void Root_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            Dismiss();
            e.Handled = true;
        }
    }

    private void Window_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            Dismiss();
        }
    }

    private void Dismiss()
    {
        _visible = false;
        AppWindow.Hide();
    }

    private void ThemeChanged(object? sender, ThemeChangedEventArgs e) =>
        DispatcherQueue.TryEnqueue(() => Root.RequestedTheme = _themeService.Current.Theme);

    private void SettingsChanged(ISettingsService sender, SettingsModel settings) =>
        DispatcherQueue.TryEnqueue(() =>
        {
            if (!_closed && _visible)
            {
                _ = RefreshAsync();
            }
        });

    private void Window_Closed(object sender, WindowEventArgs args)
    {
        _closed = true;
        _visible = false;
        _themeService.ThemeChanged -= ThemeChanged;
        _settingsService.SettingsChanged -= SettingsChanged;
        ViewModel.Dispose();
    }
}
