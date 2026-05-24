// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Common.UI.Controls
{
    public sealed partial class ShortcutWithTextLabelControl : Control
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

        public Placement LabelPlacement
        {
            get { return (Placement)GetValue(LabelPlacementProperty); }
            set { SetValue(LabelPlacementProperty, value); }
        }

        public static readonly DependencyProperty LabelPlacementProperty = DependencyProperty.Register(nameof(LabelPlacement), typeof(Placement), typeof(ShortcutWithTextLabelControl), new PropertyMetadata(defaultValue: Placement.After, OnIsLabelPlacementChanged));

        public MarkdownConfig MarkdownConfig
        {
            get { return (MarkdownConfig)GetValue(MarkdownConfigProperty); }
            set { SetValue(MarkdownConfigProperty, value); }
        }

        public static readonly DependencyProperty MarkdownConfigProperty = DependencyProperty.Register(nameof(MarkdownConfig), typeof(MarkdownConfig), typeof(ShortcutWithTextLabelControl), new PropertyMetadata(new MarkdownConfig()));

        public Style KeyVisualStyle
        {
            get { return (Style)GetValue(KeyVisualStyleProperty); }
            set { SetValue(KeyVisualStyleProperty, value); }
        }

        public static readonly DependencyProperty KeyVisualStyleProperty = DependencyProperty.Register(nameof(KeyVisualStyle), typeof(Style), typeof(ShortcutWithTextLabelControl), new PropertyMetadata(default(Style)));

        public ShortcutWithTextLabelControl()
        {
            DefaultStyleKey = typeof(ShortcutWithTextLabelControl);
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
        }

        private static void OnIsLabelPlacementChanged(DependencyObject d, DependencyPropertyChangedEventArgs newValue)
        {
            if (d is ShortcutWithTextLabelControl labelControl)
            {
                if (labelControl.LabelPlacement == Placement.Before)
                {
                    VisualStateManager.GoToState(labelControl, "LabelBefore", true);
                }
                else
                {
                    VisualStateManager.GoToState(labelControl, "LabelAfter", true);
                }
            }
        }

        public enum Placement
        {
            Before,
            After,
        }
    }
}
