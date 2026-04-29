// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using PowerAccent.Core.Tools;
using Windows.Graphics;
using WinRT.Interop;

using Point = PowerAccent.Core.Point;
using Size = PowerAccent.Core.Size;

namespace PowerAccent.UI;

public sealed partial class Selector : Window, IDisposable
{
    private readonly Core.PowerAccent _powerAccent = new();
    private readonly OverlappedPresenter _presenter;
    private readonly nint _hwnd;

    private bool _disposed;
    private int _selectedIndex;

    public Selector()
    {
        InitializeComponent();

        _hwnd = WindowNative.GetWindowHandle(this);

        // Borderless, non-resizable, non-modal tool window. Replaces WPF's
        // WindowStyle="None" / ResizeMode="NoResize" / ShowInTaskbar="False".
        _presenter = OverlappedPresenter.CreateForToolWindow();
        _presenter.SetBorderAndTitleBar(false, false);
        _presenter.IsResizable = false;
        _presenter.IsMaximizable = false;
        _presenter.IsMinimizable = false;
        _presenter.IsAlwaysOnTop = false;
        AppWindow.SetPresenter(_presenter);

        // WS_EX_NOACTIVATE + WS_EX_TOOLWINDOW: never steal focus from the user's typing target,
        // never appear in taskbar / Alt-Tab. The whole point of QuickAccent is that typing
        // continues uninterrupted while the picker floats above.
        WindowsFunctions.ApplyNoActivateWindowStyle(_hwnd);

        AppWindow.Hide();

        _powerAccent.OnChangeDisplay += PowerAccent_OnChangeDisplay;
        _powerAccent.OnSelectCharacter += PowerAccent_OnSelectionCharacter;

        Closed += Selector_Closed;
    }

    private void PowerAccent_OnSelectionCharacter(int index, string character)
    {
        _selectedIndex = index;
        characters.SelectedIndex = _selectedIndex;
        characterName.Text = _powerAccent.CharacterDescriptions[_selectedIndex];
        characters.ScrollIntoView(character);
    }

    private void PowerAccent_OnChangeDisplay(bool isActive, string[] chars)
    {
        // Topmost is conditionally set here to address hybrid graphics issues on laptops.
        _presenter.IsAlwaysOnTop = isActive;

        characterNameContainer.Visibility = _powerAccent.ShowUnicodeDescription
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (isActive)
        {
            characters.ItemsSource = chars;
            characters.SelectedIndex = _selectedIndex;

            if (Content is FrameworkElement root)
            {
                root.UpdateLayout();
            }

            SizeAndPositionWindow();
            WindowsFunctions.ShowWindowNoActivate(_hwnd);
        }
        else
        {
            AppWindow.Hide();
        }
    }

    private void SizeAndPositionWindow()
    {
        // Cap ListBox width to active-display width minus padding (DIPs).
        characters.MaxWidth = _powerAccent.GetDisplayMaxWidth();

        if (Content is not FrameworkElement root)
        {
            return;
        }

        root.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        var desired = root.DesiredSize;

        // GetDpiForWindow returns 96 for unscaled, 192 for 200%, etc.
        var dpi = WindowsFunctions.GetDpiForWindowSafe(_hwnd) / 96.0;

        var widthPx = (int)Math.Ceiling(desired.Width * dpi);
        var heightPx = (int)Math.Ceiling(desired.Height * dpi);
        if (widthPx <= 0 || heightPx <= 0)
        {
            return;
        }

        // GetDisplayCoordinates expects/returns DIPs.
        var sizeDip = new Size(desired.Width, desired.Height);
        Point position = _powerAccent.GetDisplayCoordinates(sizeDip);

        var posX = (int)Math.Round(position.X * dpi);
        var posY = (int)Math.Round(position.Y * dpi);

        AppWindow.MoveAndResize(new RectInt32(posX, posY, widthPx, heightPx));
    }

    private void Selector_Closed(object sender, WindowEventArgs args)
    {
        _powerAccent.SaveUsageInfo();
        Dispose();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _powerAccent.Dispose();
        GC.SuppressFinalize(this);
    }
}
