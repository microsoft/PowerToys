// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;

namespace Microsoft.PowerToys.Common.UI.Controls
{
    [TemplatePart(Name = KeyPresenter, Type = typeof(KeyCharPresenter))]
    [TemplateVisualState(Name = NormalState, GroupName = "CommonStates")]
    [TemplateVisualState(Name = DisabledState, GroupName = "CommonStates")]
    [TemplateVisualState(Name = InvalidState, GroupName = "CommonStates")]
    [TemplateVisualState(Name = WarningState, GroupName = "CommonStates")]
    public sealed partial class KeyVisual : Control
    {
        private const string KeyPresenter = "KeyPresenter";
        private const string NormalState = "Normal";
        private const string DisabledState = "Disabled";
        private const string InvalidState = "Invalid";
        private const string WarningState = "Warning";
        private KeyCharPresenter _keyPresenter = null!;

        public object Content
        {
            get => (object)GetValue(ContentProperty);
            set => SetValue(ContentProperty, value);
        }

        public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(nameof(Content), typeof(object), typeof(KeyVisual), new PropertyMetadata(default(string), OnContentChanged));

        public State State
        {
            get => (State)GetValue(StateProperty);
            set => SetValue(StateProperty, value);
        }

        public static readonly DependencyProperty StateProperty = DependencyProperty.Register(nameof(State), typeof(State), typeof(KeyVisual), new PropertyMetadata(State.Normal, OnStateChanged));

        public bool RenderKeyAsGlyph
        {
            get => (bool)GetValue(RenderKeyAsGlyphProperty);
            set => SetValue(RenderKeyAsGlyphProperty, value);
        }

        public static readonly DependencyProperty RenderKeyAsGlyphProperty = DependencyProperty.Register(nameof(RenderKeyAsGlyph), typeof(bool), typeof(KeyVisual), new PropertyMetadata(false, OnContentChanged));

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
            var keyVisual = (KeyVisual)d;
            keyVisual.Update();
            keyVisual.SetVisualStates();
        }

        private static void OnStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((KeyVisual)d).SetVisualStates();
        }

        private void SetVisualStates()
        {
            if (this != null)
            {
                if (State == State.Error)
                {
                    VisualStateManager.GoToState(this, InvalidState, true);
                }
                else if (State == State.Warning)
                {
                    VisualStateManager.GoToState(this, WarningState, true);
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
            if (_keyPresenter == null)
            {
                return;
            }

            _keyPresenter.Content = Content;
            _keyPresenter.Style = (Style)Application.Current.Resources["DefaultKeyCharPresenterStyle"];

            if (Content == null)
            {
                return;
            }

            if (Content is string key)
            {
                switch (key)
                {
                    case "Copilot":
                        _keyPresenter.Style = (Style)Application.Current.Resources["CopilotKeyCharPresenterStyle"];
                        break;

                    case "Office":
                        _keyPresenter.Style = (Style)Application.Current.Resources["OfficeKeyCharPresenterStyle"];
                        break;

                    default:
                        _keyPresenter.Style = (Style)Application.Current.Resources["DefaultKeyCharPresenterStyle"];
                        break;
                }

                return;
            }

            if (Content is int keyCode)
            {
                VirtualKey virtualKey = (VirtualKey)keyCode;
                switch (virtualKey)
                {
                    case VirtualKey.Enter:
                        SetGlyphOrText("\uE751", virtualKey);
                        break;

                    case VirtualKey.Back:
                        SetGlyphOrText("\uE750", virtualKey);
                        break;

                    case VirtualKey.Shift:
                    case (VirtualKey)160: // Left Shift
                    case (VirtualKey)161: // Right Shift
                        SetGlyphOrText("\uE752", virtualKey);
                        break;

                    case VirtualKey.Up:
                        SetGlyph("\uE0E4");
                        break;

                    case VirtualKey.Down:
                        SetGlyph("\uE0E5");
                        break;

                    case VirtualKey.Left:
                        SetGlyph("\uE0E2");
                        break;

                    case VirtualKey.Right:
                        SetGlyph("\uE0E3");
                        break;

                    case VirtualKey.LeftWindows:
                    case VirtualKey.RightWindows:
                        _keyPresenter.Style = (Style)Application.Current.Resources["WindowsKeyCharPresenterStyle"];
                        break;
                }
            }
        }

        private void SetGlyphOrText(string glyph, VirtualKey key)
        {
            if (RenderKeyAsGlyph)
            {
                SetGlyph(glyph);
            }
            else
            {
                _keyPresenter.Content = key.ToString();
                _keyPresenter.Style = (Style)Application.Current.Resources["DefaultKeyCharPresenterStyle"];
            }
        }

        private void SetGlyph(string glyph)
        {
            _keyPresenter.Content = glyph;
            _keyPresenter.Style = (Style)Application.Current.Resources["GlyphKeyCharPresenterStyle"];
        }

        private void KeyVisual_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            SetVisualStates();
        }
    }

    public enum State
    {
        Normal,
        Error,
        Warning,
    }
}
