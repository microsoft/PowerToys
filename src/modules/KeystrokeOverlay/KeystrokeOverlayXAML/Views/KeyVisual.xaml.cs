// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

// This UserControl displays a single key visual, matching the preview
// in KeystrokeOverlayPage.xaml.
namespace KeystrokeOverlayUI
{
    public sealed partial class KeyVisual : UserControl
    {
        public KeyVisual()
        {
            this.InitializeComponent();
        }

        // --- KeyText DependencyProperty ---
        // This will hold the text displayed on the key (e.g., "Ctrl", "K")
        public string KeyText
        {
            get { return (string)GetValue(KeyTextProperty); }
            set { SetValue(KeyTextProperty, value); }
        }

        public static readonly DependencyProperty KeyTextProperty =
            DependencyProperty.Register(nameof(KeyText), typeof(string), typeof(KeyVisual), new PropertyMetadata(string.Empty));

        // --- KeyFontSize DependencyProperty ---
        // Binds to the TextSize setting
        public double KeyFontSize { get; set; } = 12.0; // or any valid positive number

        public static readonly DependencyProperty KeyFontSizeProperty =
            DependencyProperty.Register(nameof(KeyFontSize), typeof(double), typeof(KeyVisual), new PropertyMetadata(24.0));

        // --- KeyBackground DependencyProperty ---
        // Binds to the BackgroundColor + BackgroundOpacity settings
        public Brush KeyBackground
        {
            get { return (Brush)GetValue(KeyBackgroundProperty); }
            set { SetValue(KeyBackgroundProperty, value); }
        }

        public static readonly DependencyProperty KeyBackgroundProperty =
            DependencyProperty.Register(nameof(KeyBackground), typeof(Brush), typeof(KeyVisual), new PropertyMetadata(null));

        // --- KeyForeground DependencyProperty ---
        // Binds to the TextColor + TextOpacity settings
        public Brush KeyForeground
        {
            get { return (Brush)GetValue(KeyForegroundProperty); }
            set { SetValue(KeyForegroundProperty, value); }
        }

        public static readonly DependencyProperty KeyForegroundProperty =
            DependencyProperty.Register(nameof(KeyForeground), typeof(Brush), typeof(KeyVisual), new PropertyMetadata(null));
    }
}
