// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Labs.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using WinUIEx;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public partial class FlyoutMenuButton : Button
    {
        /// <summary>
        /// The backing <see cref="DependencyProperty"/> for the <see cref="Icon"/> property.
        /// </summary>
        public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
            nameof(Icon),
            typeof(object),
            typeof(FlyoutMenuButton),
            new PropertyMetadata(defaultValue: null));

        /// <summary>
        /// Gets or sets the icon.
        /// </summary>
        public object Icon
        {
            get => (object)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public FlyoutMenuButton()
        {
            this.DefaultStyleKey = typeof(FlyoutMenuButton);
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
        }
    }
}
