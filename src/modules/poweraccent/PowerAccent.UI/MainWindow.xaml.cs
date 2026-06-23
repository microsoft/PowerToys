// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using WinRT.Interop;
using WinUIEx;
using Point = PowerAccent.Core.Point;
using Size = PowerAccent.Core.Size;

namespace PowerAccent.UI;

public sealed partial class MainWindow : WindowEx, IDisposable, INotifyPropertyChanged
{
    private const SET_WINDOW_POS_FLAGS WindowPosFlags =
        SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE;

    private readonly Core.PowerAccent _powerAccent;
    private Visibility _characterNameVisibility = Visibility.Visible;
    private int _selectedIndex = -1;

    public event PropertyChangedEventHandler PropertyChanged;

    public Visibility CharacterNameVisibility
    {
        get => _characterNameVisibility;
        set
        {
            _characterNameVisibility = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CharacterNameVisibility)));
        }
    }

    public MainWindow()
    {
        InitializeComponent();

        var dispatchQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        _powerAccent = new Core.PowerAccent(action => dispatchQueue.TryEnqueue(() => action()));
        _powerAccent.OnChangeDisplay += PowerAccent_OnChangeDisplay;
        _powerAccent.OnSelectCharacter += PowerAccent_OnSelectionCharacter;

        var hwnd = this.GetWindowHandle();
        if (hwnd != IntPtr.Zero)
        {
            var exStyle = (WINDOW_EX_STYLE)PInvoke.GetWindowLong((HWND)hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
            exStyle |= WINDOW_EX_STYLE.WS_EX_NOACTIVATE;
            exStyle |= WINDOW_EX_STYLE.WS_EX_TOOLWINDOW;
            exStyle |= WINDOW_EX_STYLE.WS_EX_TRANSPARENT;
            PInvoke.SetWindowLong((HWND)hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, (int)exStyle);
        }

        AppWindow.IsShownInSwitchers = false;
        AppWindow.Hide();

        Closed += (s, e) =>
        {
            _powerAccent.SaveUsageInfo();
            _powerAccent.Dispose();
        };
    }

    private void PowerAccent_OnSelectionCharacter(int index, string character)
    {
        _selectedIndex = index;
        CharactersList.SelectedIndex = _selectedIndex;

        if (_selectedIndex >= 0 && _selectedIndex < _powerAccent.CharacterDescriptions.Length)
        {
            CharacterNameText.Text = _powerAccent.CharacterDescriptions[_selectedIndex];
        }

        if (CharactersList.Items.Count > _selectedIndex && _selectedIndex >= 0)
        {
            CharactersList.ScrollIntoView(CharactersList.Items[_selectedIndex]);
        }
    }

    private void PowerAccent_OnChangeDisplay(bool isActive, string[] chars)
    {
        AppWindow.IsTopMost = isActive;

        CharacterNameVisibility = _powerAccent.ShowUnicodeDescription ? Visibility.Visible : Visibility.Collapsed;

        if (isActive)
        {
            int offscreenX = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_XVIRTUALSCREEN) - 1000;
            int offscreenY = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_YVIRTUALSCREEN) - 1000;

            var hwnd = this.GetWindowHandle();
            if (hwnd != IntPtr.Zero)
            {
                PInvoke.SetWindowPos((HWND)hwnd, (HWND)IntPtr.Zero, offscreenX, offscreenY, 0, 0, WindowPosFlags);
            }

            AppWindow.Show();
            SetWindowSize();
            CharactersList.ItemsSource = chars;
            CharactersList.SelectedIndex = -1;

            CharactersList.UpdateLayout();

            CharactersList.SelectedIndex = _selectedIndex;

            if (_selectedIndex >= 0 && _selectedIndex < chars.Length)
            {
                CharacterNameText.Text = _powerAccent.CharacterDescriptions[_selectedIndex];
                CharactersList.ScrollIntoView(CharactersList.Items[_selectedIndex]);
                CharactersList.UpdateLayout();
            }
            else
            {
                CharacterNameText.Text = string.Empty;
            }

            SetWindowPosition();
            Microsoft.PowerToys.Telemetry.PowerToysTelemetry.Log.WriteEvent(new Core.Telemetry.PowerAccentShowAccentMenuEvent());
        }
        else
        {
            AppWindow.Hide();
            CharactersList.ItemsSource = null;
            _selectedIndex = -1;
        }
    }

    private void SetWindowPosition()
    {
        double actualWidth = ContentBorder.ActualWidth;
        double actualHeight = ContentBorder.ActualHeight;
        Size windowSize = new Size(actualWidth, actualHeight);
        Point physicalPosition = _powerAccent.GetDisplayCoordinates(windowSize);

        var hwnd = this.GetWindowHandle();
        if (hwnd != IntPtr.Zero)
        {
            PInvoke.SetWindowPos((HWND)hwnd, (HWND)IntPtr.Zero, (int)Math.Round(physicalPosition.X), (int)Math.Round(physicalPosition.Y), 0, 0, WindowPosFlags);
        }
    }

    private void SetWindowSize()
    {
        double maxWidth = _powerAccent.GetDisplayMaxWidth();
        CharactersList.MaxWidth = maxWidth;
        this.MaxWidth = maxWidth;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
