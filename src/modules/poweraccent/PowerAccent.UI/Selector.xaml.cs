// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Windows;

using Wpf.Ui.Controls;

using Point = PowerAccent.Core.Point;
using Size = PowerAccent.Core.Size;

namespace PowerAccent.UI;

public partial class Selector : FluentWindow, IDisposable, INotifyPropertyChanged
{
    private readonly Core.PowerAccent _powerAccent = new();

    private Visibility _characterNameVisibility = Visibility.Visible;

    private int _selectedIndex;

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
        Application.Current.MainWindow.Topmost = true;
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
        characterName.Text = _powerAccent.CharacterDescriptions[_selectedIndex];
    }

    private void PowerAccent_OnChangeDisplay(bool isActive, string[] chars)
    {
        CharacterNameVisibility = _powerAccent.ShowUnicodeDescription ? Visibility.Visible : Visibility.Collapsed;

        if (isActive)
        {
            characters.ItemsSource = chars;
            characters.SelectedIndex = _selectedIndex;
            this.UpdateLayout(); // Required for filling the actual width/height before positioning.
            SetWindowPosition();
            Show();
            Microsoft.PowerToys.Telemetry.PowerToysTelemetry.Log.WriteEvent(new PowerAccent.Core.Telemetry.PowerAccentShowAccentMenuEvent());
        }
        else
        {
            Hide();
        }
    }

    private void MenuExit_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void SetWindowPosition()
    {
        Size windowSize = new(((System.Windows.Controls.Panel)Application.Current.MainWindow.Content).ActualWidth, ((System.Windows.Controls.Panel)Application.Current.MainWindow.Content).ActualHeight);
        Point position = _powerAccent.GetDisplayCoordinates(windowSize);
        this.Left = position.X;
        this.Top = position.Y;
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
