// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.AnimatedVisuals;
using Microsoft.UI.Xaml.Media;

namespace Microsoft.PowerToys.Settings.UI.Behaviors
{
    public sealed class CheckBoxAnimationBehavior : DependencyObject
    {
        public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
            "IsEnabled", typeof(bool), typeof(CheckBoxAnimationBehavior), new PropertyMetadata(false, OnIsEnabledChanged));

        private static readonly DependencyProperty SubscriptionProperty = DependencyProperty.RegisterAttached(
            "Subscription", typeof(object), typeof(CheckBoxAnimationBehavior), new PropertyMetadata(null));

        public static bool GetIsEnabled(CheckBox checkBox) => (bool)checkBox.GetValue(IsEnabledProperty);

        public static void SetIsEnabled(CheckBox checkBox, bool value) => checkBox.SetValue(IsEnabledProperty, value);

        private static void OnIsEnabledChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (sender is not CheckBox checkBox)
            {
                return;
            }

            (checkBox.GetValue(SubscriptionProperty) as Subscription)?.Dispose();
            checkBox.ClearValue(SubscriptionProperty);
            if ((bool)args.NewValue)
            {
                checkBox.SetValue(SubscriptionProperty, new Subscription(checkBox));
            }
        }

        private sealed class Subscription : IDisposable
        {
            private readonly CheckBox _checkBox;
            private AnimatedIcon _glyph;
            private long _glyphToken;
            private long _templateToken;
            private bool _listening;
            private bool _refreshQueued;
            private bool _disposed;

            public Subscription(CheckBox checkBox)
            {
                _checkBox = checkBox;
                _checkBox.Loaded += OnLoaded;
                _checkBox.Unloaded += OnUnloaded;
                if (_checkBox.IsLoaded)
                {
                    Start();
                }
            }

            public void Dispose()
            {
                _disposed = true;
                _checkBox.Loaded -= OnLoaded;
                _checkBox.Unloaded -= OnUnloaded;
                Stop();
            }

            private void OnLoaded(object sender, RoutedEventArgs args) => Start();

            private void OnUnloaded(object sender, RoutedEventArgs args) => Stop();

            private void Start()
            {
                if (_listening || _disposed)
                {
                    return;
                }

                _listening = true;
                _templateToken = _checkBox.RegisterPropertyChangedCallback(Control.TemplateProperty, OnTemplateChanged);
                AttachGlyph();
            }

            private void Stop()
            {
                if (_listening)
                {
                    _checkBox.UnregisterPropertyChangedCallback(Control.TemplateProperty, _templateToken);
                    _listening = false;
                }

                DetachGlyph();
            }

            private void OnTemplateChanged(DependencyObject sender, DependencyProperty property)
            {
                DetachGlyph();
                if (!_refreshQueued)
                {
                    _refreshQueued = _checkBox.DispatcherQueue.TryEnqueue(() =>
                    {
                        _refreshQueued = false;
                        if (_listening && !_disposed && _checkBox.IsLoaded)
                        {
                            AttachGlyph();
                        }
                    });
                }
            }

            private void AttachGlyph()
            {
                _checkBox.ApplyTemplate();
                if (VisualTreeHelper.GetChildrenCount(_checkBox) == 0)
                {
                    return;
                }

                var root = VisualTreeHelper.GetChild(_checkBox, 0) as FrameworkElement;
                var glyph = root?.FindName("CheckGlyph") as AnimatedIcon;
                if (glyph?.Source is not AnimatedAcceptVisualSource || ReferenceEquals(glyph, _glyph))
                {
                    return;
                }

                DetachGlyph();
                _glyph = glyph;
                _glyphToken = _glyph.RegisterPropertyChangedCallback(AnimatedIcon.StateProperty, OnGlyphStateChanged);
                OnGlyphStateChanged(_glyph, AnimatedIcon.StateProperty);
            }

            private void DetachGlyph()
            {
                if (_glyph != null)
                {
                    _glyph.UnregisterPropertyChangedCallback(AnimatedIcon.StateProperty, _glyphToken);
                    _glyph = null;
                }
            }

            private void OnGlyphStateChanged(DependencyObject sender, DependencyProperty property)
            {
                // An ancestor can replace the unchecked glyph's state with generic Normal.
                // AnimatedAccept requires NormalOff; leave its other native transitions intact.
                if (_checkBox.IsChecked == false && AnimatedIcon.GetState(sender) == "Normal")
                {
                    AnimatedIcon.SetState(sender, "NormalOff");
                }
            }
        }
    }
}
