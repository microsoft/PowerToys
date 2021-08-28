// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Markup;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    [TemplatePart(Name = KeyPresenter, Type = typeof(ContentPresenter))]
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

        public KeyVisual()
        {
            this.DefaultStyleKey = typeof(KeyVisual);
            this.Style = (Style)App.Current.Resources["DefaultKeyVisualStyle"];
        }

        protected override void OnApplyTemplate()
        {
            _keyVisual = (KeyVisual)this;
            _keyPresenter = (ContentPresenter)_keyVisual.GetTemplateChild(KeyPresenter);
            Update();
            base.OnApplyTemplate();
        }

        private static void OnContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((KeyVisual)d).Update();
        }

        private void Update()
        {
            if (_keyVisual == null)
            {
                return;
            }

            System.Diagnostics.Debug.WriteLine(_keyVisual.Style);

            if (_keyVisual.Content != null)
            {
                if (_keyVisual.Content.GetType() == typeof(string))
                {
                    _keyVisual.Style = (Style)App.Current.Resources["DefaultKeyVisualStyle"];
                    _keyVisual._keyPresenter.Content = _keyVisual.Content;
                }
                else
                {
                    IconElement icon;

                    switch ((int)_keyVisual.Content)
                     {
                        case 13: // The Enter key or button.
                            icon = new FontIcon() { Glyph = "\uE751" }; _keyVisual.Style = (Style)App.Current.Resources["WideIconKeyVisualStyle"]; break;

                        case 8: // The Back key or button.
                            icon = new FontIcon() { Glyph = "\uE750" }; _keyVisual.Style = (Style)App.Current.Resources["WideIconKeyVisualStyle"]; break;

                        case 16: // The right Shift key or button.
                        case 160: // The left Shift key or button.
                        case 161: // The Shift key or button.
                            icon = new FontIcon() { Glyph = "\uE752" }; _keyVisual.Style = (Style)App.Current.Resources["WideIconKeyVisualStyle"]; break;

                        case 91: // The left Windows key
                        case 92: // The right Windows key

                            icon = XamlReader.Load(@"<PathIcon xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" Data=""M9,17V9h8v8ZM0,17V9H8v8ZM9,8V0h8V8ZM0,8V0H8V8Z"" />") as PathIcon;
                            _keyVisual.Style = (Style)App.Current.Resources["IconKeyVisualStyle"]; break;

                        case 38: icon = new FontIcon() { Glyph = "\uE0E4" }; _keyVisual.Style = (Style)App.Current.Resources["IconKeyVisualStyle"]; break; // The Up Arrow key or button.
                        case 40: icon = new FontIcon() { Glyph = "\uE0E5" }; _keyVisual.Style = (Style)App.Current.Resources["IconKeyVisualStyle"]; break; // The Down Arrow key or button.
                        case 37: icon = new FontIcon() { Glyph = "\uE0E2" }; _keyVisual.Style = (Style)App.Current.Resources["IconKeyVisualStyle"]; break;  // The Left Arrow key or button.
                        case 39: icon = new FontIcon() { Glyph = "\uE0E3" }; _keyVisual.Style = (Style)App.Current.Resources["IconKeyVisualStyle"]; break; // The Right Arrow key or button.

                        default: icon = new FontIcon() { Glyph = ((VirtualKey)_keyVisual.Content).ToString() }; _keyVisual.Style = (Style)App.Current.Resources["DefaultKeyVisualStyle"]; break;
                    }

                    _keyVisual._keyPresenter.Content = icon;
                }
            }
        }
    }
}
