// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    [TemplatePart(Name = KeyPresenter, Type = typeof(ContentPresenter))]
    [TemplateVisualState(Name = "Normal", GroupName = "CommonStates")]
    [TemplateVisualState(Name = "Disabled", GroupName = "CommonStates")]
    [TemplateVisualState(Name = "Error", GroupName = "CommonStates")]
    public sealed partial class KeyVisual : Control
    {
        private const string KeyPresenter = "KeyPresenter";
        private KeyVisual _keyVisual;
        private ContentPresenter _keyPresenter;

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
            _keyVisual = (KeyVisual)this;
            _keyPresenter = (ContentPresenter)_keyVisual.GetTemplateChild(KeyPresenter);
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
            if (_keyVisual != null)
            {
                if (_keyVisual.IsInvalid)
                {
                    VisualStateManager.GoToState(_keyVisual, "Invalid", true);
                }
                else if (!_keyVisual.IsEnabled)
                {
                    VisualStateManager.GoToState(_keyVisual, "Disabled", true);
                }
                else
                {
                    VisualStateManager.GoToState(_keyVisual, "Normal", true);
                }
            }
        }

        private void Update()
        {
            if (_keyVisual != null && _keyVisual.Content != null)
            {
                if (_keyVisual.Content.GetType() == typeof(string))
                {
                    _keyVisual._keyPresenter.Content = _keyVisual.Content;
                    _keyVisual._keyPresenter.Margin = new Thickness(0, -1, 0, 0);
                }
                else
                {
                    _keyVisual._keyPresenter.FontSize = _keyVisual.FontSize * 0.8;
                    _keyVisual._keyPresenter.FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
                    switch ((int)_keyVisual.Content)
                    {
                        case 13: // The Enter key or button.
                            _keyVisual._keyPresenter.Content = "\uE751"; break;

                        case 8: // The Back key or button.
                            _keyVisual._keyPresenter.Content = "\uE750"; break;

                        case 16: // The right Shift key or button.
                        case 160: // The left Shift key or button.
                        case 161: // The Shift key or button.
                            _keyVisual._keyPresenter.Content = "\uE752"; break;

                        case 38: _keyVisual._keyPresenter.Content = "\uE0E4"; break; // The Up Arrow key or button.
                        case 40: _keyVisual._keyPresenter.Content = "\uE0E5"; break; // The Down Arrow key or button.
                        case 37: _keyVisual._keyPresenter.Content = "\uE0E2"; break; // The Left Arrow key or button.
                        case 39: _keyVisual._keyPresenter.Content = "\uE0E3"; break; // The Right Arrow key or button.

                        case 91: // The left Windows key
                        case 92: // The right Windows key
                            PathIcon winIcon = XamlReader.Load(@"<PathIcon xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" Data=""M896 896H0V0h896v896zm1024 0h-896V0h896v896zM896 1920H0v-896h896v896zm1024 0h-896v-896h896v896z"" />") as PathIcon;
                            Viewbox winIconContainer = new Viewbox();
                            winIconContainer.Child = winIcon;
                            winIconContainer.HorizontalAlignment = HorizontalAlignment.Center;
                            winIconContainer.VerticalAlignment = VerticalAlignment.Center;

                            double iconDimensions = _keyVisual.FontSize * 0.8; // Adjust the size of the icon based on the font size
                            winIconContainer.Height = iconDimensions;
                            winIconContainer.Width = iconDimensions;
                            _keyVisual._keyPresenter.Content = winIconContainer;
                            break;
                        default: _keyVisual._keyPresenter.Content = ((VirtualKey)_keyVisual.Content).ToString(); break;
                    }
                }
            }
        }

        private void KeyVisual_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            SetVisualStates();
        }
    }
}
