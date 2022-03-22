// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.WinUI3.Controls
{
    public sealed partial class ShortcutWithTextLabelControl : UserControl
    {
        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string), typeof(ShortcutWithTextLabelControl), new PropertyMetadata(default(string)));

#pragma warning disable CA2227 // Collection properties should be read only
        public List<object> Keys
#pragma warning restore CA2227 // Collection properties should be read only
        {
            get { return (List<object>)GetValue(KeysProperty); }
            set { SetValue(KeysProperty, value); }
        }

        public static readonly DependencyProperty KeysProperty = DependencyProperty.Register("Keys", typeof(List<object>), typeof(ShortcutWithTextLabelControl), new PropertyMetadata(default(string)));

        public ShortcutWithTextLabelControl()
        {
            this.InitializeComponent();
        }
    }
}
