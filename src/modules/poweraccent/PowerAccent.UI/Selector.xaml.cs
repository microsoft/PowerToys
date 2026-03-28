// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Windows;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

using Wpf.Ui.Controls;

using Point = PowerAccent.Core.Point;
using Size = PowerAccent.Core.Size;

namespace PowerAccent.UI;

public partial class Selector : FluentWindow, IDisposable, INotifyPropertyChanged
{
    // When setting the position for the selector window, we do not alter the z-order,
    // activation status, or size.
    private const SET_WINDOW_POS_FLAGS WindowPosFlags =
        SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE;

    private readonly Core.PowerAccent _powerAccent = new();

    private Visibility _characterNameVisibility = Visibility.Visible;

    private int _selectedIndex = -1;

    public event PropertyChangedEventHandler PropertyChanged;

    public Visibility CharacterNameVisibility
    {
        get
        {
            return _characterNameVisibility;
        }

        set
        {
            _characterNameVisibility = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CharacterNameVisibility)));
        }
    }

    public Selector()
    {
        InitializeComponent();

        Wpf.Ui.Appearance.SystemThemeWatcher.Watch(this);

        Application.Current.MainWindow.ShowActivated = false;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _powerAccent.OnChangeDisplay += PowerAccent_OnChangeDisplay;
        _powerAccent.OnSelectCharacter += PowerAccent_OnSelectionCharacter;
        this.Visibility = Visibility.Hidden;
    }

    private void PowerAccent_OnSelectionCharacter(int index, string character)
    {
        _selectedIndex = index;
        characters.SelectedIndex = _selectedIndex;

        if (_selectedIndex >= 0 && _selectedIndex < _powerAccent.CharacterDescriptions.Length)
        {
            characterName.Text = _powerAccent.CharacterDescriptions[_selectedIndex];
        }

        if (characters.Items.Count > _selectedIndex && _selectedIndex >= 0)
        {
            characters.ScrollIntoView(characters.Items[_selectedIndex]);
        }
    }

    private void PowerAccent_OnChangeDisplay(bool isActive, string[] chars)
    {
        // Topmost is conditionally set here to address hybrid graphics issues on laptops.
        this.Topmost = isActive;

        CharacterNameVisibility = _powerAccent.ShowUnicodeDescription ? Visibility.Visible : Visibility.Collapsed;

        if (isActive)
        {
            int offscreenX = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_XVIRTUALSCREEN) - 1000;
            int offscreenY = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_YVIRTUALSCREEN) - 1000;

            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                // Move off-screen to avoid flicker on previous monitor before Show() and
                // UpdateLayout().
                PInvoke.SetWindowPos((HWND)hwnd, (HWND)IntPtr.Zero, offscreenX, offscreenY, 0, 0, WindowPosFlags);
            }
            else
            {
                this.Left = offscreenX;
                this.Top = offscreenY;
            }

            Show();
            SetWindowsSize();
            characters.ItemsSource = chars;
            characters.SelectedIndex = -1; // Reset before setting dynamically to avoid flashing

            this.UpdateLayout(); // Required for filling the actual width/height before positioning.

            characters.SelectedIndex = _selectedIndex;

            if (_selectedIndex >= 0 && _selectedIndex < chars.Length)
            {
                characterName.Text = _powerAccent.CharacterDescriptions[_selectedIndex];
                characters.ScrollIntoView(characters.Items[_selectedIndex]);
                this.UpdateLayout(); // Re-layout after scrolling
            }

            SetWindowPosition();
            Microsoft.PowerToys.Telemetry.PowerToysTelemetry.Log.WriteEvent(new PowerAccent.Core.Telemetry.PowerAccentShowAccentMenuEvent());
        }
        else
        {
            Hide();
            characters.ItemsSource = null;
            _selectedIndex = -1;
        }
    }

    private void MenuExit_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void SetWindowPosition()
    {
        Size windowSize = new(((System.Windows.Controls.Panel)Application.Current.MainWindow.Content).ActualWidth, ((System.Windows.Controls.Panel)Application.Current.MainWindow.Content).ActualHeight);
        Point physicalPosition = _powerAccent.GetDisplayCoordinates(windowSize);

        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
        {
            PInvoke.SetWindowPos((HWND)hwnd, (HWND)IntPtr.Zero, (int)Math.Round(physicalPosition.X), (int)Math.Round(physicalPosition.Y), 0, 0, WindowPosFlags);
        }
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        if (this.Visibility == Visibility.Visible)
        {
            SetWindowsSize();
            SetWindowPosition();
        }
    }

    private void SetWindowsSize()
    {
        double maxWidth = _powerAccent.GetDisplayMaxWidth();
        this.characters.MaxWidth = maxWidth;
        this.MaxWidth = maxWidth;
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (this.Visibility == Visibility.Visible)
        {
            SetWindowPosition();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _powerAccent.SaveUsageInfo();
        _powerAccent.Dispose();
        base.OnClosed(e);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
