// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.UI.Controls;

public sealed partial class ContentPropertyControl : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(ContentPropertyViewModel), typeof(ContentPropertyControl), new PropertyMetadata(null));

    public ContentPropertyViewModel? ViewModel
    {
        get => (ContentPropertyViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public ContentPropertyControl()
    {
        InitializeComponent();
    }

    private void Control_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Use the available content width, not the containing window. Every row
        // in a property grid receives the same width and label column.
        var columns = e.NewSize.Width >= 420;
        LabelColumn.Width = columns ? new GridLength(140) : new GridLength(1, GridUnitType.Star);
        ValueColumn.Width = columns ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        LabelText.Margin = columns ? new Thickness(0, 0, 12, 0) : new Thickness(0, 0, 0, 4);
        Grid.SetColumn(ValueContent, columns ? 1 : 0);
        Grid.SetRow(ValueContent, columns ? 0 : 1);
    }
}
