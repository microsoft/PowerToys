// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using Microsoft.UI.Xaml.Controls;

namespace ShortcutGuide;

public sealed partial class TaskbarIndicator : UserControl
{
    private int _indicatorNumber;

    public int IndicatorNumber
    {
        get => _indicatorNumber;
        set
        {
            _indicatorNumber = value;
            IndicatorText.Text = value > 9 ? "0" : value.ToString(CultureInfo.InvariantCulture);
        }
    }

    public new double Width
    {
        get => (double)GetValue(WidthProperty);
        set
        {
            SetValue(WidthProperty, value);
            IndicatorText.Width = Width;
            IndicatorCanvas.Width = Width;
            IndicatorRectangle.Width = Width;
        }
    }

    public new double Height
    {
        get => (double)GetValue(HeightProperty);
        set
        {
            SetValue(HeightProperty, value);
            IndicatorText.Height = Height;
            IndicatorCanvas.Height = Height;
            IndicatorRectangle.Height = Height;
        }
    }

    public TaskbarIndicator()
    {
        InitializeComponent();
    }
}
