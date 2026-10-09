// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PowerDisplay;

public sealed partial class MonitorIcon : UserControl
{
    public MonitorIcon()
    {
        InitializeComponent();
    }

    public bool IsBuiltIn
    {
        get => (bool)GetValue(IsBuiltInProperty);
        set => SetValue(IsBuiltInProperty, value);
    }

    public static readonly DependencyProperty IsBuiltInProperty = DependencyProperty.Register(nameof(IsBuiltIn), typeof(bool), typeof(MonitorIcon), new PropertyMetadata(false, OnPropertyChanged));

    public int MonitorNumber
    {
        get => (int)GetValue(MonitorNumberProperty);
        set => SetValue(MonitorNumberProperty, value);
    }

    public static readonly DependencyProperty MonitorNumberProperty = DependencyProperty.Register(nameof(MonitorNumber), typeof(int), typeof(MonitorIcon), new PropertyMetadata(0, OnPropertyChanged));

    private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var monIcon = (MonitorIcon)d;
        if (monIcon.IsBuiltIn)
        {
            VisualStateManager.GoToState(monIcon, "BuiltIn", true);
        }
        else
        {
            VisualStateManager.GoToState(monIcon, "Monitor", true);
        }
    }
}
