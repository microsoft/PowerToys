// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public sealed partial class ShortcutTextControl : UserControl
    {
        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public ShortcutTextControl()
        {
            this.InitializeComponent();
        }

        public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string), typeof(ShortcutVisualControl), new PropertyMetadata(default(string), (s, e) =>
        {
            var self = (ShortcutTextControl)s;
            var parts = Regex.Split(e.NewValue.ToString(), @"({[\s\S]+?})").Where(l => !string.IsNullOrEmpty(l)).ToArray();

            foreach (var seg in parts)
            {
                if (!string.IsNullOrWhiteSpace(seg))
                {
                    if (seg.Contains("{", StringComparison.InvariantCulture))
                    {
                        Run key = new Run()
                        {
                            Text = Regex.Replace(seg, @"[{}]", string.Empty),
                            FontWeight = FontWeights.SemiBold,
                        };
                        self.ContentText.Inlines.Add(key);
                    }
                    else
                    {
                        Run description = new Run()
                        {
                            Text = seg,
                            FontWeight = FontWeights.Normal,
                        };
                        self.ContentText.Inlines.Add(description);
                    }
                }
            }
        }));
    }
}
