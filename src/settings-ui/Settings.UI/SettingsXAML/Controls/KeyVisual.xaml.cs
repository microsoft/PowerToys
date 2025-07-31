// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.System;
using Windows.UI;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    [TemplatePart(Name = KeyPresenter, Type = typeof(ContentPresenter))]
    [TemplateVisualState(Name = NormalState, GroupName = "CommonStates")]
    [TemplateVisualState(Name = DisabledState, GroupName = "CommonStates")]
    [TemplateVisualState(Name = InvalidState, GroupName = "CommonStates")]
    public sealed partial class KeyVisual : Control
    {
        private const string KeyPresenter = "KeyPresenter";
        private const string NormalState = "Normal";
        private const string DisabledState = "Disabled";
        private const string InvalidState = "Invalid";
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

        public string AccessibleName
        {
            get => (string)GetValue(AccessibleNameProperty);
            set => SetValue(AccessibleNameProperty, value);
        }

        public static readonly DependencyProperty AccessibleNameProperty = DependencyProperty.Register(nameof(AccessibleName), typeof(string), typeof(KeyVisual), new PropertyMetadata(string.Empty, OnAccessibleNameChanged));

        public KeyVisual()
        {
            this.DefaultStyleKey = typeof(KeyVisual);
        }

        protected override void OnApplyTemplate()
        {
            IsEnabledChanged -= KeyVisual_IsEnabledChanged;
            _keyPresenter = (ContentPresenter)this.GetTemplateChild(KeyPresenter);
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

        private static void OnAccessibleNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (KeyVisual)d;
            if (control._keyPresenter != null)
            {
                AutomationProperties.SetName(control._keyPresenter, (string)e.NewValue);
            }
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
                FrameworkElement contentElement = null;
                string accessibleName = string.Empty;

                if (Content.GetType() == typeof(string))
                {
                    // For plain strings, set directly
                    _keyPresenter.Content = Content;
                    _keyPresenter.Margin = new Thickness(0, -1, 0, 0);
                    AutomationProperties.SetName(_keyPresenter, Content.ToString());
                    return;
                }

                switch ((int)Content)
                {
                    case 13: // Enter key
                        accessibleName = ResourceLoaderInstance.ResourceLoader.GetString("KeyEnter");
                        contentElement = CreateGlyphTextBlock("\uE751", accessibleName);
                        break;

                    case 8: // Back key
                        accessibleName = ResourceLoaderInstance.ResourceLoader.GetString("KeyBack");
                        contentElement = CreateGlyphTextBlock("\uE750", accessibleName);
                        break;

                    case 16: // Right Shift
                    case 160: // Left Shift
                    case 161: // Shift key
                        accessibleName = ResourceLoaderInstance.ResourceLoader.GetString("KeyShift");
                        contentElement = CreateGlyphTextBlock("\uE752", accessibleName);
                        break;

                    case 38: // Up arrow
                        accessibleName = ResourceLoaderInstance.ResourceLoader.GetString("KeyUpArrow");
                        contentElement = CreateGlyphTextBlock("\uE0E4", accessibleName);
                        break;

                    case 40: // Down arrow
                        accessibleName = ResourceLoaderInstance.ResourceLoader.GetString("KeyDownArrow");
                        contentElement = CreateGlyphTextBlock("\uE0E5", accessibleName);
                        break;

                    case 37: // Left arrow
                        accessibleName = ResourceLoaderInstance.ResourceLoader.GetString("KeyLeftArrow");
                        contentElement = CreateGlyphTextBlock("\uE0E2", accessibleName);
                        break;

                    case 39: // Right arrow
                        accessibleName = ResourceLoaderInstance.ResourceLoader.GetString("KeyRightArrow");
                        contentElement = CreateGlyphTextBlock("\uE0E3", accessibleName);
                        break;

                    case 91: // Left Windows key
                    case 92: // Right Windows key
                        accessibleName = ResourceLoaderInstance.ResourceLoader.GetString("KeyWindows");

                        Geometry geometry = GetGeometryFromAppResources("WindowsLogoData");

                        var brush = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];

                        Path winIcon = new()
                        {
                            Data = geometry,
                            Fill = brush,
                            Stretch = Stretch.Uniform,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center,
                        };

                        Viewbox winIconContainer = new()
                        {
                            Child = winIcon,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center,
                            Height = FontSize * 0.8,
                            Width = FontSize * 0.8,
                        };

                        AutomationProperties.SetName(winIconContainer, accessibleName);
                        contentElement = winIconContainer;
                        break;

                    default:
                        accessibleName = ((VirtualKey)Content).ToString();
                        contentElement = new TextBlock
                        {
                            Text = accessibleName,
                            FontSize = FontSize * 0.8,
                        };
                        AutomationProperties.SetName(contentElement, accessibleName);
                        break;
                }

                _keyPresenter.Content = contentElement;
                AccessibleName = accessibleName;
            }
        }

        private TextBlock CreateGlyphTextBlock(string glyph, string accessibleName)
        {
            var tb = new TextBlock
            {
                Text = glyph,
                FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
                FontSize = FontSize * 0.8,
            };
            AutomationProperties.SetName(tb, accessibleName);
            return tb;
        }

        private Geometry GetGeometryFromAppResources(string key)
        {
            if (Application.Current.Resources.TryGetValue(key, out var value) && value is string pathData)
            {
                return GetGeometryFromString(pathData);
            }

            throw new InvalidOperationException($"Resource with key '{key}' not found or not a string.");
        }

        private Geometry GetGeometryFromString(string pathData)
        {
            return (Geometry)XamlBindingHelper.ConvertValue(typeof(Geometry), pathData);
        }

        private void KeyVisual_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            SetVisualStates();
        }
    }
}
