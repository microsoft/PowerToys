// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Windows;
using Point = PowerAccent.Core.Point;
using Size = PowerAccent.Core.Size;

namespace PowerAccent.UI;

public partial class MainWindow : Window, IDisposable
{
    private Core.PowerAccent _powerAccent = new Core.PowerAccent();
    private Selector _selector;
    private Stack<Selector> _selectorStack = new Stack<Selector>();

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
            Selector selector = new Selector(chars);
            selector.Show();
            CenterWindow(selector);
            _selectorStack.Push(selector);
            _selector = selector;
            Microsoft.PowerToys.Telemetry.PowerToysTelemetry.Log.WriteEvent(new PowerAccent.Core.Telemetry.PowerAccentShowAccentMenuEvent());
        }
        else
        {
            while (_selectorStack.Count > 0)
            {
                _selectorStack.Pop().Close();
            }
        }
    }

    private void CenterWindow(Selector selector)
    {
        Size window = new Size(((System.Windows.Controls.Panel)selector.Content).ActualWidth, ((System.Windows.Controls.Panel)selector.Content).ActualHeight);
        Point position = _powerAccent.GetDisplayCoordinates(window);
        selector.SetPosition(position.X, position.Y);
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
