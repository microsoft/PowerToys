// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.UI;

namespace Microsoft.CmdPal.UI.Controls;

public sealed partial class ColorPalette : UserControl
{
    public static readonly DependencyProperty PaletteColorsProperty = DependencyProperty.Register(nameof(PaletteColors), typeof(ObservableCollection<Color>), typeof(ColorPalette), null!)!;

    public static readonly DependencyProperty CustomPaletteColumnCountProperty = DependencyProperty.Register(nameof(CustomPaletteColumnCount), typeof(int), typeof(ColorPalette), new PropertyMetadata(10))!;

    public static readonly DependencyProperty SelectedColorProperty = DependencyProperty.Register(nameof(SelectedColor), typeof(Color), typeof(ColorPalette), new PropertyMetadata(null!))!;

    public event EventHandler<Color?>? SelectedColorChanged;

    private Color? _selectedColor;

    public Color? SelectedColor
    {
        get => _selectedColor;

        set
        {
            if (_selectedColor != value)
            {
                _selectedColor = value;
                if (value is not null)
                {
                    SetValue(SelectedColorProperty, value);
                }
                else
                {
                    ClearValue(SelectedColorProperty);
                }
            }
        }
    }

    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "This dependency-property wrapper must remain writable so XAML and runtime bindings can replace the collection.")]
    public ObservableCollection<Color> PaletteColors
    {
        get => (ObservableCollection<Color>)GetValue(PaletteColorsProperty)!;
        set => SetValue(PaletteColorsProperty, value);
    }

    public int CustomPaletteColumnCount
    {
        get => (int)GetValue(CustomPaletteColumnCountProperty);
        set => SetValue(CustomPaletteColumnCountProperty, value);
    }

    public ColorPalette()
    {
        PaletteColors = [];
        InitializeComponent();
    }

    private void ListViewBase_OnItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is Color color)
        {
            SelectedColor = color;
            SelectedColorChanged?.Invoke(this, color);
        }
    }
}
