// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public sealed partial class TextBlockControl : UserControl
    {
        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
           "Text",
           typeof(string),
           typeof(TextBlockControl),
           null);

        [Localizable(true)]
        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public static readonly DependencyProperty EnabledForegroundProperty = DependencyProperty.Register(
           "EnabledForeground",
           typeof(Brush),
           typeof(TextBlockControl),
           null);

        public Brush EnabledForeground
        {
            get => (Brush)GetValue(EnabledForegroundProperty);
            set => SetValue(EnabledForegroundProperty, value);
        }

        public static readonly DependencyProperty DisabledForegroundProperty = DependencyProperty.Register(
           "DisabledForeground",
           typeof(Brush),
           typeof(TextBlockControl),
           null);

        public Brush DisabledForeground
        {
            get => (Brush)GetValue(DisabledForegroundProperty);
            set => SetValue(DisabledForegroundProperty, value);
        }

        public TextBlockControl()
        {
            this.InitializeComponent();
            DataContext = this;

            IsEnabledChanged += TextBlockControl_IsEnabledChanged;
        }

        private void TextBlockControl_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                textBlock.Foreground = EnabledForeground;
            }
            else
            {
                textBlock.Foreground = DisabledForeground;
            }
        }

        private void TextBlock_Loaded(object sender, RoutedEventArgs e)
        {
            textBlock.Foreground = IsEnabled ? EnabledForeground : DisabledForeground;
        }
    }
}
