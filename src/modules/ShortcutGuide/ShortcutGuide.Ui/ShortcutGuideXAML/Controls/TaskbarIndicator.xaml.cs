// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ShortcutGuide.Controls;

public sealed partial class TaskbarIndicator : UserControl
{
    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(TaskbarIndicator), new PropertyMetadata(default(string)));

    public TaskbarIndicator()
    {
        this.InitializeComponent();
    }
}
