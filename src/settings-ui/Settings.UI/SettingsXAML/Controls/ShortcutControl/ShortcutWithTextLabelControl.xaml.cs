// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public sealed partial class ShortcutWithTextLabelControl : UserControl
    {
        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(ShortcutWithTextLabelControl), new PropertyMetadata(default(string)));

        public List<object> Keys
        {
            get { return (List<object>)GetValue(KeysProperty); }
            set { SetValue(KeysProperty, value); }
        }

        public static readonly DependencyProperty KeysProperty = DependencyProperty.Register(nameof(Keys), typeof(List<object>), typeof(ShortcutWithTextLabelControl), new PropertyMetadata(default(string)));

        public LabelPlacement LabelPlacement
        {
            get { return (LabelPlacement)GetValue(LabelPlacementProperty); }
            set { SetValue(LabelPlacementProperty, value); }
        }

        public static readonly DependencyProperty LabelPlacementProperty = DependencyProperty.Register(nameof(LabelPlacement), typeof(LabelPlacement), typeof(ShortcutWithTextLabelControl), new PropertyMetadata(defaultValue: LabelPlacement.After, OnIsLabelPlacementChanged));

        public ShortcutWithTextLabelControl()
        {
            this.InitializeComponent();
        }

        private static void OnIsLabelPlacementChanged(DependencyObject d, DependencyPropertyChangedEventArgs newValue)
        {
            if (d is ShortcutWithTextLabelControl labelControl)
            {
                if (labelControl.LabelPlacement == LabelPlacement.Before)
                {
                  VisualStateManager.GoToState(labelControl, "LabelBefore", true);
                }
                else
                {
                    VisualStateManager.GoToState(labelControl, "LabelAfter", true);
                }
            }
        }
    }

    public enum LabelPlacement
    {
        Before,
        After,
    }
}
