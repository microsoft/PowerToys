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

        public bool RenderSmall
        {
            get => (bool)GetValue(RenderSmallStyleProperty);
            set => SetValue(RenderSmallStyleProperty, value);
        }

        public static readonly DependencyProperty RenderSmallStyleProperty = DependencyProperty.Register("RenderSmall", typeof(bool), typeof(KeyVisual), new PropertyMetadata(false, OnRenderSmallChanged));

        public KeyVisual()
        {
            this.DefaultStyleKey = typeof(KeyVisual);
            this.Style = GetStyleSize("TextKeyVisualStyle");
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

        private static void OnRenderSmallChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
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
                    _keyVisual.Style = GetStyleSize("TextKeyVisualStyle");
                    _keyVisual._keyPresenter.Content = _keyVisual.Content;
                }
                else
                {
                    _keyVisual.Style = GetStyleSize("IconKeyVisualStyle");

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
                            PathIcon winIcon = XamlReader.Load(@"<PathIcon xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" Data=""M9,17V9h8v8ZM0,17V9H8v8ZM9,8V0h8V8ZM0,8V0H8V8Z"" />") as PathIcon;
                            winIcon.HorizontalAlignment = HorizontalAlignment.Center;
                            winIcon.VerticalAlignment = VerticalAlignment.Center;

                            _keyVisual._keyPresenter.Content = winIcon;
                            break;
                        default: _keyVisual._keyPresenter.Content = ((VirtualKey)_keyVisual.Content).ToString();  break;
                    }
                }
            }
        }

        public Style GetStyleSize(string styleName)
        {
            if (RenderSmall)
            {
                return (Style)App.Current.Resources["Small" + styleName];
            }
            else
            {
                return (Style)App.Current.Resources["Default" + styleName];
            }
        }

        public double GetIconSize()
        {
            if (RenderSmall)
            {
                return (double)App.Current.Resources["SmallIconSize"];
            }
            else
            {
                return (double)App.Current.Resources["DefaultIconSize"];
            }
        }
    }
}
