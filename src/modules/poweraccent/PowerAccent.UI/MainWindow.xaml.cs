// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows;
using Point = PowerAccent.Core.Point;
using Size = PowerAccent.Core.Size;

namespace PowerAccent.UI;

public partial class MainWindow : Window, IDisposable
{
    private Core.PowerAccent _powerAccent = new Core.PowerAccent();
    private Selector _selector;

    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _powerAccent.OnChangeDisplay += PowerAccent_OnChangeDisplay;
        _powerAccent.OnSelectCharacter += PowerAccent_OnSelectionCharacter;
        this.Visibility = Visibility.Hidden;
    }

    private void PowerAccent_OnSelectionCharacter(int index)
    {
        _selector?.SetIndex(index);
    }

    private void PowerAccent_OnChangeDisplay(bool isActive, string[] chars)
    {
        if (isActive)
        {
            _selector = new Selector(chars);
            _selector.Show();
            CenterWindow();
            Microsoft.PowerToys.Telemetry.PowerToysTelemetry.Log.WriteEvent(new PowerAccent.Core.Telemetry.PowerAccentShowAccentMenuEvent());
        }
        else
        {
            _selector.Close();
        }
    }

    private void CenterWindow()
    {
        Size window = new Size(((System.Windows.Controls.Panel)_selector.Content).ActualWidth, ((System.Windows.Controls.Panel)_selector.Content).ActualHeight);
        Point position = _powerAccent.GetDisplayCoordinates(window);
        _selector.SetPosition(position.X, position.Y);
    }

    protected override void OnClosed(EventArgs e)
    {
        _powerAccent.Dispose();
        base.OnClosed(e);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
