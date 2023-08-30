// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.IO;
using System.Text;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public sealed partial class FancyZonesPreviewControl : UserControl
    {
        public FancyZonesPreviewControl()
        {
            this.InitializeComponent();

            try
            {
                var wallpaperPathBuilder = new StringBuilder(260);
                NativeMethods.SystemParametersInfo(NativeMethods.SPI_GETDESKWALLPAPER, wallpaperPathBuilder.Capacity, wallpaperPathBuilder, 0);
                var wallpaperPath = wallpaperPathBuilder.ToString();
                if (File.Exists(wallpaperPath))
                {
                    WallpaperPath = wallpaperPath;
                }
            }
            catch (Win32Exception)
            {
            }
        }

        private void FancyZonesPreviewControl_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            SetEnabledState();
        }

        public bool IsSystemTheme
        {
            get { return (bool)GetValue(IsSystemThemeProperty); }
            set { SetValue(IsSystemThemeProperty, value); }
        }

        public static readonly DependencyProperty IsSystemThemeProperty = DependencyProperty.Register("IsSystemTheme", typeof(bool), typeof(FancyZonesPreviewControl), new PropertyMetadata(default(bool), OnPropertyChanged));

        public string WallpaperPath
        {
            get { return (string)GetValue(WallpaperPathProperty); }
            set { SetValue(WallpaperPathProperty, value); }
        }

        public static readonly DependencyProperty WallpaperPathProperty = DependencyProperty.Register("WallpaperPath", typeof(string), typeof(FancyZonesPreviewControl), new PropertyMetadata("ms-appx:///Assets/Settings/Modules/Wallpaper.png"));

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

        public Color CustomNumberColor
        {
            get { return (Color)GetValue(CustomNumberColorProperty); }
            set { SetValue(CustomNumberColorProperty, value); }
        }

        public static readonly DependencyProperty CustomNumberColorProperty = DependencyProperty.Register("CustomNumberColor", typeof(Color), typeof(FancyZonesPreviewControl), new PropertyMetadata(null, OnPropertyChanged));

        public bool ShowZoneNumber
        {
            get { return (bool)GetValue(ShowZoneNumberProperty); }
            set { SetValue(ShowZoneNumberProperty, value); }
        }

        public static readonly DependencyProperty ShowZoneNumberProperty = DependencyProperty.Register("ShowZoneNumber", typeof(bool), typeof(FancyZonesPreviewControl), new PropertyMetadata(default(bool), OnPropertyChanged));

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
        private SolidColorBrush numberBrush;

        private void Update()
        {
            if (!IsSystemTheme)
            {
                highlightBrush = new SolidColorBrush(CustomHighlightColor);
                inActiveBrush = new SolidColorBrush(CustomInActiveColor);
                borderBrush = new SolidColorBrush(CustomBorderColor);
                numberBrush = new SolidColorBrush(CustomNumberColor);
            }
            else
            {
                highlightBrush = (SolidColorBrush)this.Resources["DefaultAccentBrush"];
                inActiveBrush = (SolidColorBrush)this.Resources["SolidBackgroundBrush"];
                borderBrush = (SolidColorBrush)this.Resources["DefaultBorderBrush"];
                numberBrush = (SolidColorBrush)this.Resources["SolidZoneNumberBrush"];
            }

            highlightBrush.Opacity = HighlightOpacity / 100;
            inActiveBrush.Opacity = HighlightOpacity / 100;
            Zone1.Background = highlightBrush;
            Zone2.Background = inActiveBrush;
            Zone3.Background = inActiveBrush;

            Zone1.BorderBrush = borderBrush;
            Zone2.BorderBrush = borderBrush;
            Zone3.BorderBrush = borderBrush;

            Zone1Number.Foreground = numberBrush;
            Zone2Number.Foreground = numberBrush;
            Zone3Number.Foreground = numberBrush;
        }

        private void SetEnabledState()
        {
            VisualStateManager.GoToState(this, IsEnabled ? "Normal" : "Disabled", true);
        }

        private void FancyZonesPreviewControl_Loaded(object sender, RoutedEventArgs e)
        {
            IsEnabledChanged -= FancyZonesPreviewControl_IsEnabledChanged;
            SetEnabledState();
            IsEnabledChanged += FancyZonesPreviewControl_IsEnabledChanged;
        }
    }
}
