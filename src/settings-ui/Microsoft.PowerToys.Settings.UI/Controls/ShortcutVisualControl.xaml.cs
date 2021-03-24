// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public sealed partial class ShortcutVisualControl : UserControl
    {
        public ShortcutVisualControl()
        {
            InitializeComponent();
        }

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string), typeof(ShortcutVisualControl), new PropertyMetadata(default(string), (s, e) =>
        {
            var self = (ShortcutVisualControl)s;
            var parts = Regex.Split(e.NewValue.ToString(), @"({[\s\S]+?})").Where(l => !string.IsNullOrEmpty(l)).ToArray();

            foreach (var seg in parts)
            {
                if (!string.IsNullOrWhiteSpace(seg))
                {
                    if (seg.Contains("{", StringComparison.InvariantCulture))
                    {
                        KeyVisual k = new KeyVisual
                        {
                            Content = Regex.Replace(seg, @"[{}]", string.Empty),
                            VerticalAlignment = VerticalAlignment.Center,
                        };

                        self.contentPanel.Children.Add(k);
                    }
                    else
                    {
                        TextBlock t = new TextBlock
                        {
                            Text = seg,
                            TextWrapping = TextWrapping.Wrap,
                            VerticalAlignment = VerticalAlignment.Top,
                            Margin = new Thickness(0, 6, 0, 0),
                        };

                        self.contentPanel.Children.Add(t);
                    }
                }
            }
        }));
    }
}
