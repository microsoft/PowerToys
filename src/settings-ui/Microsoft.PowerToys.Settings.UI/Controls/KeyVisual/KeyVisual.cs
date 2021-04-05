// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public sealed class KeyVisual : Control
    {
        public object Content
        {
            get => (string)GetValue(ContentProperty);
            set => SetValue(ContentProperty, value);
        }

        public static readonly DependencyProperty ContentProperty = DependencyProperty.Register("Content", typeof(object), typeof(KeyVisual), new PropertyMetadata(default(string)));

        public KeyVisual()
        {
            this.DefaultStyleKey = typeof(KeyVisual);
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
        }
    }
}
