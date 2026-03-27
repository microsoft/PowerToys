// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using KeyboardManagerEditorUI.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace KeyboardManagerEditorUI.Controls
{
    [TemplatePart(Name = TypeIconPart, Type = typeof(FontIcon))]
    [TemplatePart(Name = LabelTextPart, Type = typeof(TextBlock))]
    public sealed partial class IconLabelControl : Control
    {
        private const string TypeIconPart = "TypeIcon";
        private const string LabelTextPart = "LabelText";

        private FontIcon? _typeIcon;
        private TextBlock? _labelText;

        public static readonly DependencyProperty ActionTypeProperty =
            DependencyProperty.Register(
                nameof(ActionType),
                typeof(ActionType),
                typeof(IconLabelControl),
                new PropertyMetadata(ActionType.Text, OnActionTypeChanged));

        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register(
                nameof(Label),
                typeof(string),
                typeof(IconLabelControl),
                new PropertyMetadata(string.Empty));

        public ActionType ActionType
        {
            get => (ActionType)GetValue(ActionTypeProperty);
            set => SetValue(ActionTypeProperty, value);
        }

        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        public IconLabelControl()
        {
            this.DefaultStyleKey = typeof(IconLabelControl);
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _typeIcon = GetTemplateChild(TypeIconPart) as FontIcon;
            _labelText = GetTemplateChild(LabelTextPart) as TextBlock;

            UpdateIcon();
        }

        private static void OnActionTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is IconLabelControl control)
            {
                control.UpdateIcon();
            }
        }

        private void UpdateIcon()
        {
            if (_typeIcon == null)
            {
                return;
            }

            _typeIcon.Glyph = ActionType switch
            {
                ActionType.Program => "\uECAA",
                ActionType.Text => "\uE8D2",
                ActionType.Shortcut => "\uEDA7",
                ActionType.MouseClick => "\uE962",
                ActionType.Url => "\uE774",
                _ => "\uE8A5",
            };
        }
    }
}
