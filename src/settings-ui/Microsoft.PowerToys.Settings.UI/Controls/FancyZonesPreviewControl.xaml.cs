// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public sealed partial class FancyZonesPreviewControl : UserControl
    {
        public FancyZonesPreviewControl()
        {
            this.InitializeComponent();
        }

        public bool IsSystemTheme
        {
            get { return (bool)GetValue(IsSystemThemeProperty); }
            set { SetValue(IsSystemThemeProperty, value); }
        }

        public static readonly DependencyProperty IsSystemThemeProperty = DependencyProperty.Register("IsSystemTheme", typeof(bool), typeof(FancyZonesPreviewControl), new PropertyMetadata(default(bool), OnPropertyChanged));

        public Color CustomBorderColor
        {
            get { return (Color)GetValue(CustomBorderColorProperty); }
            set { SetValue(CustomBorderColorProperty, value); }
        }

        public static readonly DependencyProperty CustomBorderColorProperty = DependencyProperty.Register("CustomBorderColor", typeof(Color), typeof(FancyZonesPreviewControl), new PropertyMetadata(null, OnPropertyChanged));

        public Color CustomInActiveColor
        {
            get { return (Color)GetValue(CustomInActiveColorProperty); }
            set { SetValue(CustomInActiveColorProperty, value); }
        }

        public static readonly DependencyProperty CustomInActiveColorProperty = DependencyProperty.Register("CustomInActiveColor", typeof(Color), typeof(FancyZonesPreviewControl), new PropertyMetadata(null, OnPropertyChanged));

        public Color CustomHighlightColor
        {
            get { return (Color)GetValue(CustomHighlightColorProperty); }
            set { SetValue(CustomHighlightColorProperty, value); }
        }

        public static readonly DependencyProperty CustomHighlightColorProperty = DependencyProperty.Register("CustomHighlightColor", typeof(Color), typeof(FancyZonesPreviewControl), new PropertyMetadata(null, OnPropertyChanged));

        public double HighlightOpacity
        {
            get { return (double)GetValue(HighlightOpacityProperty); }
            set { SetValue(HighlightOpacityProperty, value); }
        }

        public static readonly DependencyProperty HighlightOpacityProperty = DependencyProperty.Register("CustomHighlightColor", typeof(double), typeof(FancyZonesPreviewControl), new PropertyMetadata(1.0, OnPropertyChanged));

        private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((FancyZonesPreviewControl)d).Update();
        }

        private SolidColorBrush highlightBrush;
        private SolidColorBrush inActiveBrush;
        private SolidColorBrush borderBrush;

        private void Update()
        {
            if (!IsSystemTheme)
            {
                highlightBrush = new SolidColorBrush(CustomHighlightColor);
                inActiveBrush = new SolidColorBrush(CustomInActiveColor);
                borderBrush = new SolidColorBrush(CustomBorderColor);
            }
            else
            {
                highlightBrush = (SolidColorBrush)this.Resources["DefaultAccentBrush"];
                inActiveBrush = (SolidColorBrush)this.Resources["SolidBackgroundBrush"];
                borderBrush = (SolidColorBrush)this.Resources["DefaultBorderBrush"];
            }

            highlightBrush.Opacity = HighlightOpacity / 100;
            inActiveBrush.Opacity = HighlightOpacity / 100;
            Zone1.Background = highlightBrush;
            Zone2.Background = inActiveBrush;
            Zone3.Background = inActiveBrush;

            Zone1.BorderBrush = borderBrush;
            Zone2.BorderBrush = borderBrush;
            Zone3.BorderBrush = borderBrush;
        }
    }
}
