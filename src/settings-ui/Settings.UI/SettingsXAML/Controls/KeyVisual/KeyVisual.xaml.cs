// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    [TemplatePart(Name = KeyPresenter, Type = typeof(KeyCharPresenter))]
    [TemplateVisualState(Name = NormalState, GroupName = "CommonStates")]
    [TemplateVisualState(Name = DisabledState, GroupName = "CommonStates")]
    [TemplateVisualState(Name = InvalidState, GroupName = "CommonStates")]
    public sealed partial class KeyVisual : Control
    {
        private const string KeyPresenter = "KeyPresenter";
        private const string NormalState = "Normal";
        private const string DisabledState = "Disabled";
        private const string InvalidState = "Invalid";
        private KeyCharPresenter _keyPresenter;

        public object Content
        {
            get => (object)GetValue(ContentProperty);
            set => SetValue(ContentProperty, value);
        }

        public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(nameof(Content), typeof(object), typeof(KeyVisual), new PropertyMetadata(default(string), OnContentChanged));

        public bool IsInvalid
        {
            get => (bool)GetValue(IsInvalidProperty);
            set => SetValue(IsInvalidProperty, value);
        }

        public static readonly DependencyProperty IsInvalidProperty = DependencyProperty.Register(nameof(IsInvalid), typeof(bool), typeof(KeyVisual), new PropertyMetadata(false, OnIsInvalidChanged));

        public KeyVisual()
        {
            this.DefaultStyleKey = typeof(KeyVisual);
        }

        protected override void OnApplyTemplate()
        {
            IsEnabledChanged -= KeyVisual_IsEnabledChanged;
            _keyPresenter = (KeyCharPresenter)this.GetTemplateChild(KeyPresenter);
            Update();
            SetVisualStates();
            IsEnabledChanged += KeyVisual_IsEnabledChanged;
            base.OnApplyTemplate();
        }

        private static void OnContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((KeyVisual)d).SetVisualStates();
        }

        private static void OnIsInvalidChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((KeyVisual)d).SetVisualStates();
        }

        private void SetVisualStates()
        {
            if (this != null)
            {
                if (IsInvalid)
                {
                    VisualStateManager.GoToState(this, InvalidState, true);
                }
                else if (!IsEnabled)
                {
                    VisualStateManager.GoToState(this, DisabledState, true);
                }
                else
                {
                    VisualStateManager.GoToState(this, NormalState, true);
                }
            }
        }

        private void Update()
        {
            if (this != null && this.Content != null)
            {
                string accessibleName = string.Empty;
                _keyPresenter.Content = Content;

                if (Content is int symbol)
                {
                    _keyPresenter.Style = (Style)Application.Current.Resources["GlyphKeyCharPresenterStyle"];

                    switch (symbol)
                    {
                        case 13: // Enter key
                            _keyPresenter.Content = "\uE751";
                            break;

                        case 8: // Back key
                            _keyPresenter.Content = "\uE750";
                            break;

                        case 16: // Right Shift
                        case 160: // Left Shift
                        case 161: // Shift key
                            _keyPresenter.Content = "\uE752";
                            break;

                        case 38: // Up arrow
                            _keyPresenter.Content = "\uE0E4";
                            break;

                        case 40: // Down arrow
                            _keyPresenter.Content = "\uE0E5";
                            break;

                        case 37: // Left arrow
                            _keyPresenter.Content = "\uE0E2";
                            break;

                        case 39: // Right arrow
                            _keyPresenter.Content = "\uE0E3";
                            break;

                        case 91: // Left Windows key
                        case 92: // Right Windows key
                            _keyPresenter.Style = (Style)Application.Current.Resources["WindowsKeyCharPresenterStyle"];
                            break;
                    }
                }
                else
                {
                    _keyPresenter.Style = (Style)Application.Current.Resources["DefaultKeyCharPresenterStyle"];
                }
            }
        }

        private void KeyVisual_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            SetVisualStates();
        }
    }
}
