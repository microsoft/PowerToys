// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Windows.System;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    [TemplatePart(Name = KeyPresenter, Type = typeof(ContentPresenter))]
    [TemplateVisualState(Name = "Normal", GroupName = "CommonStates")]
    [TemplateVisualState(Name = "Disabled", GroupName = "CommonStates")]
    [TemplateVisualState(Name = "Default", GroupName = "StateStates")]
    [TemplateVisualState(Name = "Error", GroupName = "StateStates")]
    public sealed class KeyVisual : Control
    {
        private const string KeyPresenter = "KeyPresenter";
        private KeyVisual _keyVisual;
        private ContentPresenter _keyPresenter;

        public object Content
        {
            get => (object)GetValue(ContentProperty);
            set => SetValue(ContentProperty, value);
        }

        public static readonly DependencyProperty ContentProperty = DependencyProperty.Register("Content", typeof(object), typeof(KeyVisual), new PropertyMetadata(default(string), OnContentChanged));

        public bool IsError
        {
            get => (bool)GetValue(IsErrorProperty);
            set => SetValue(IsErrorProperty, value);
        }

        public static readonly DependencyProperty IsErrorProperty = DependencyProperty.Register("IsError", typeof(bool), typeof(KeyVisual), new PropertyMetadata(false, OnIsErrorChanged));

        public KeyVisual()
        {
            this.DefaultStyleKey = typeof(KeyVisual);
        }

        protected override void OnApplyTemplate()
        {
            IsEnabledChanged -= KeyVisual_IsEnabledChanged;
            _keyVisual = (KeyVisual)this;
            _keyPresenter = (ContentPresenter)_keyVisual.GetTemplateChild(KeyPresenter);
            Update();
            SetEnabledState();
            SetErrorState();
            IsEnabledChanged += KeyVisual_IsEnabledChanged;
            base.OnApplyTemplate();
        }

        private static void OnContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((KeyVisual)d).Update();
        }

        private static void OnSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((KeyVisual)d).Update();
        }

        private static void OnIsErrorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((KeyVisual)d).SetErrorState();
        }

        private void Update()
        {
            if (_keyVisual == null)
            {
                return;
            }

            if (_keyVisual.Content != null)
            {
                if (_keyVisual.Content.GetType() == typeof(string))
                {
                    _keyVisual._keyPresenter.Content = _keyVisual.Content;
                }
                else
                {
                    switch ((int)_keyVisual.Content)
                    {
                        /* We can enable other glyphs in the future
                        case 13: // The Enter key or button.
                            _keyVisual._keyPresenter.Content = "\uE751"; break;

                        case 8: // The Back key or button.
                            _keyVisual._keyPresenter.Content = "\uE750"; break;

                        case 16: // The right Shift key or button.
                        case 160: // The left Shift key or button.
                        case 161: // The Shift key or button.
                            _keyVisual._keyPresenter.Content = "\uE752"; break; */

                        case 38: _keyVisual._keyPresenter.Content = "\uE0E4"; break; // The Up Arrow key or button.
                        case 40: _keyVisual._keyPresenter.Content = "\uE0E5"; break; // The Down Arrow key or button.
                        case 37: _keyVisual._keyPresenter.Content = "\uE0E2"; break; // The Left Arrow key or button.
                        case 39: _keyVisual._keyPresenter.Content = "\uE0E3"; break; // The Right Arrow key or button.

                        case 91: // The left Windows key
                        case 92: // The right Windows key
                            PathIcon winIcon = XamlReader.Load(@"<PathIcon xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" Data=""M683 683H0V0H683V683ZM1502 683H819V0H1502V683ZM683 1502H0V819H683V1502ZM1502 1502H819V819H1502V1502Z"" />") as PathIcon;
                            Viewbox winIconContainer = new();
                            winIconContainer.Child = winIcon;
                            winIconContainer.MinWidth = 10;
                            winIconContainer.MaxWidth = this.FontSize;
                            winIconContainer.HorizontalAlignment = HorizontalAlignment.Center;
                            winIconContainer.VerticalAlignment = VerticalAlignment.Center;
                            _keyVisual._keyPresenter.Content = winIconContainer;
                            break;
                        default: _keyVisual._keyPresenter.Content = ((VirtualKey)_keyVisual.Content).ToString(); break;
                    }
                }
            }
        }

        private void KeyVisual_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            SetEnabledState();
        }

        private void SetErrorState()
        {
            VisualStateManager.GoToState(this, IsError ? "Error" : "Default", true);
        }

        private void SetEnabledState()
        {
            VisualStateManager.GoToState(this, IsEnabled ? "Normal" : "Disabled", true);
        }
    }
}
