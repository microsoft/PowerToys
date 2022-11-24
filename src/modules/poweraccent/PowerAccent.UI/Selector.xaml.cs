// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;

namespace PowerAccent.UI;

public partial class Selector : Window
{
    public Selector(string[] selectedCharacters)
    {
        InitializeComponent();
        this.ShowActivated = false;
        this.Topmost = true;
        characters.ItemsSource = selectedCharacters;
        characters.SelectedIndex = 0;
    }

    public void SetIndex(int index)
    {
        characters.SelectedIndex = index;
    }

    public void SetPosition(double left, double top)
    {
        this.Left = left;
        this.Top = top;
    }
}
