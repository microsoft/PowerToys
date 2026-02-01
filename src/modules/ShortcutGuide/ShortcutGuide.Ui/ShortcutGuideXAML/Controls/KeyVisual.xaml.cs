// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;

namespace ShortcutGuide.Controls
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
        private KeyCharPresenter _keyPresenter = null!;

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
            this._keyPresenter = (KeyCharPresenter)this.GetTemplateChild(KeyPresenter);
            this.Update();
            this.SetVisualStates();
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
                if (this.IsInvalid)
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
            if (this.Content == null)
            {
                Visibility = Visibility.Collapsed;
                return;
            }

            if (this.Content is string key)
            {
                SetGlyphOrText(key switch
                {
                    "<TASKBAR1-9>" => "Num",
                    "<Left>" => "\uE0E2",
                    "<Right>" => "\uE0E3",
                    "<Up>" => "\uE0E4",
                    "<Down>" => "\uE0E5",
                    "<ArrowUD>" => "\uE0E4\uE0E5",
                    "<ArrowLR>" => "\uE0E2\uE0E3",
                    "<Arrow>" => "\uE0E2\uE0E3\uE0E4\uE0E5",
                    "<Enter>" => "\uE751",
                    "<Backspace>" => "\uE750",
                    "<Escape>" => "Esc",
                    string s when s.StartsWith('<') => s.Trim('<', '>'),
                    _ => key,
                });

                this._keyPresenter.Style = key switch
                {
                    "<Copilot>" => (Style)Application.Current.Resources["CopilotKeyCharPresenterStyle"],
                    "<Office>" => (Style)Application.Current.Resources["OfficeKeyCharPresenterStyle"],
                    "<Underlined letter>" => (Style)Application.Current.Resources["UnderlinedLetterKeyCharPresenterStyle"],
                    _ => this._keyPresenter.Style,
                };

                return;
            }

            if (this.Content is int keyCode)
            {
                VirtualKey virtualKey = (VirtualKey)keyCode;
                switch (virtualKey)
                {
                    case VirtualKey.Enter:
                        this.SetGlyphOrText("\uE751");
                        break;

                    case VirtualKey.Back:
                        this.SetGlyphOrText("\uE750");
                        break;

                    case VirtualKey.Shift:
                    case (VirtualKey)160: // Left Shift
                    case (VirtualKey)161: // Right Shift
                        this.SetGlyphOrText("\uE752");
                        break;

                    case VirtualKey.Up:
                        this.SetGlyphOrText("\uE0E4");
                        break;

                    case VirtualKey.Down:
                        this.SetGlyphOrText("\uE0E5");
                        break;

                    case VirtualKey.Left:
                        this.SetGlyphOrText("\uE0E2");
                        break;

                    case VirtualKey.Right:
                        this.SetGlyphOrText("\uE0E3");
                        break;

                    case VirtualKey.LeftWindows:
                    case VirtualKey.RightWindows:
                        this._keyPresenter.Style = (Style)Application.Current.Resources["WindowsKeyCharPresenterStyle"];
                        break;
                    default: // For all other keys, we will use the key name.
                        SetGlyphOrText(virtualKey.ToString());
                        break;
                }

                return;
            }

            Visibility = Visibility.Collapsed;
        }

        private void SetGlyphOrText(string glyphOrText)
        {
            this.RenderKeyAsGlyph = ((glyphOrText[0] >> 12) & 0xF) is 0xE or 0xF;
            this._keyPresenter.Content = glyphOrText;

            this._keyPresenter.Style = this.RenderKeyAsGlyph
                ? (Style)Application.Current.Resources["GlyphKeyCharPresenterStyle"]
                : (Style)Application.Current.Resources["DefaultKeyCharPresenterStyle"];
        }

        private void KeyVisual_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            this.SetVisualStates();
        }
    }
}
