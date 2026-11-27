// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Microsoft.PowerToys.Settings.UI.Converters
{
    /// <summary>
    /// Colors an edge row's letter: the selected brush while the row's edge is selected, as its badge
    /// on the map is, and the normal brush otherwise. Both brushes are set with ThemeResource on the
    /// icon itself, so they follow the icon's theme rather than the application's.
    /// </summary>
    public static class CursorBrakeLetterBrush
    {
        public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.RegisterAttached(
            "IsSelected", typeof(bool), typeof(CursorBrakeLetterBrush), new PropertyMetadata(false, OnChanged));

        public static readonly DependencyProperty NormalBrushProperty = DependencyProperty.RegisterAttached(
            "NormalBrush", typeof(Brush), typeof(CursorBrakeLetterBrush), new PropertyMetadata(null, OnChanged));

        public static readonly DependencyProperty SelectedBrushProperty = DependencyProperty.RegisterAttached(
            "SelectedBrush", typeof(Brush), typeof(CursorBrakeLetterBrush), new PropertyMetadata(null, OnChanged));

        public static bool GetIsSelected(IconElement icon) => (bool)icon.GetValue(IsSelectedProperty);

        public static void SetIsSelected(IconElement icon, bool value) => icon.SetValue(IsSelectedProperty, value);

        public static Brush GetNormalBrush(IconElement icon) => (Brush)icon.GetValue(NormalBrushProperty);

        public static void SetNormalBrush(IconElement icon, Brush value) => icon.SetValue(NormalBrushProperty, value);

        public static Brush GetSelectedBrush(IconElement icon) => (Brush)icon.GetValue(SelectedBrushProperty);

        public static void SetSelectedBrush(IconElement icon, Brush value) => icon.SetValue(SelectedBrushProperty, value);

        private static void OnChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is IconElement icon)
            {
                var brush = GetIsSelected(icon) ? GetSelectedBrush(icon) : GetNormalBrush(icon);
                if (brush != null)
                {
                    icon.Foreground = brush;
                }
            }
        }
    }
}
