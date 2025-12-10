// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using ManagedCommon;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.UI;

namespace Microsoft.CmdPal.UI.Controls;

public sealed partial class ColorPickerButton : UserControl
{
    public static readonly DependencyProperty PaletteColorsProperty = DependencyProperty.Register(nameof(PaletteColors), typeof(ObservableCollection<Color>), typeof(ColorPickerButton), new PropertyMetadata(new ObservableCollection<Color>()))!;

    public static readonly DependencyProperty SelectedColorProperty = DependencyProperty.Register(nameof(SelectedColor), typeof(Color), typeof(ColorPickerButton), new PropertyMetadata(Colors.Black))!;

    public static readonly DependencyProperty IsAlphaEnabledProperty = DependencyProperty.Register(nameof(IsAlphaEnabled), typeof(bool), typeof(ColorPickerButton), new PropertyMetadata(defaultValue: false))!;

    public static readonly DependencyProperty IsValueEditorEnabledProperty = DependencyProperty.Register(nameof(IsValueEditorEnabled), typeof(bool), typeof(ColorPickerButton), new PropertyMetadata(false))!;

    public static readonly DependencyProperty HasSelectedColorProperty = DependencyProperty.Register(nameof(HasSelectedColor), typeof(bool), typeof(ColorPickerButton), new PropertyMetadata(false))!;

    private Color _selectedColor;

    public Color SelectedColor
    {
        get
        {
            return _selectedColor;
        }

        set
        {
            if (_selectedColor != value)
            {
                _selectedColor = value;
                SetValue(SelectedColorProperty, value);
                HasSelectedColor = true;
            }
        }
    }

    public bool HasSelectedColor
    {
        get { return (bool)GetValue(HasSelectedColorProperty); }
        set { SetValue(HasSelectedColorProperty, value); }
    }

    public bool IsAlphaEnabled
    {
        get => (bool)GetValue(IsAlphaEnabledProperty);
        set => SetValue(IsAlphaEnabledProperty, value);
    }

    public bool IsValueEditorEnabled
    {
        get { return (bool)GetValue(IsValueEditorEnabledProperty); }
        set { SetValue(IsValueEditorEnabledProperty, value); }
    }

    public ObservableCollection<Color> PaletteColors
    {
        get { return (ObservableCollection<Color>)GetValue(PaletteColorsProperty); }
        set { SetValue(PaletteColorsProperty, value); }
    }

    public ColorPickerButton()
    {
        this.InitializeComponent();

        IsEnabledChanged -= ColorPickerButton_IsEnabledChanged;
        SetEnabledState();
        IsEnabledChanged += ColorPickerButton_IsEnabledChanged;
    }

    private void ColorPickerButton_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        SetEnabledState();
    }

    private void SetEnabledState()
    {
        if (this.IsEnabled)
        {
            ColorPreviewBorder.Opacity = 1;
        }
        else
        {
            ColorPreviewBorder.Opacity = 0.2;
        }
    }

    private void ColorPalette_OnSelectedColorChanged(object? sender, Color? e)
    {
        if (e.HasValue)
        {
            HasSelectedColor = true;
            SelectedColor = e.Value;
        }
    }

    private void FlyoutBase_OnOpened(object? sender, object e)
    {
        if (sender is not Flyout flyout || (flyout.Content as FrameworkElement)?.Parent is not FlyoutPresenter flyoutPresenter)
        {
            return;
        }

        FlyoutRoot!.UpdateLayout();
        flyoutPresenter.UpdateLayout();

        // Logger.LogInfo($"FlyoutBase_OnOpened: {flyoutPresenter}, {FlyoutRoot!.ActualWidth}");
        flyoutPresenter.MaxWidth = FlyoutRoot!.ActualWidth;
        flyoutPresenter.MinWidth = 660;
        flyoutPresenter.Width = FlyoutRoot!.ActualWidth;
    }

    private void FlyoutRoot_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if ((ColorPickerFlyout!.Content as FrameworkElement)?.Parent is not FlyoutPresenter flyoutPresenter)
        {
            return;
        }

        FlyoutRoot!.UpdateLayout();
        flyoutPresenter.UpdateLayout();

        flyoutPresenter.MaxWidth = FlyoutRoot!.ActualWidth;
        flyoutPresenter.MinWidth = 660;
        flyoutPresenter.Width = FlyoutRoot!.ActualWidth;
    }

    private Thickness ToDropDownPadding(bool hasColor)
    {
        return hasColor ? new Thickness(3, 3, 8, 3) : new Thickness(8, 4, 8, 4);
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        HasSelectedColor = false;
        ColorPickerFlyout?.Hide();
    }
}
