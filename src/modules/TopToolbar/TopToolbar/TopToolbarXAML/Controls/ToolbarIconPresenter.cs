// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TopToolbar.Helpers;
using TopToolbar.Models;
using Windows.UI;

namespace TopToolbar.Controls
{
    public sealed partial class ToolbarIconPresenter : Grid
    {
        private ToolbarButton _button;
        private XamlRoot _xamlRoot;

        public ToolbarIconPresenter()
        {
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        public ToolbarButton Button
        {
            get => (ToolbarButton)GetValue(ButtonProperty);
            set => SetValue(ButtonProperty, value);
        }

        public static readonly DependencyProperty ButtonProperty = DependencyProperty.Register(
            nameof(Button),
            typeof(ToolbarButton),
            typeof(ToolbarIconPresenter),
            new PropertyMetadata(null, OnButtonChanged));

        public double IconSize
        {
            get => (double)GetValue(IconSizeProperty);
            set => SetValue(IconSizeProperty, value);
        }

        public static readonly DependencyProperty IconSizeProperty = DependencyProperty.Register(
            nameof(IconSize),
            typeof(double),
            typeof(ToolbarIconPresenter),
            new PropertyMetadata(32d, OnIconSizeChanged));

        public Color? Foreground
        {
            get => (Color?)GetValue(ForegroundProperty);
            set => SetValue(ForegroundProperty, value);
        }

        public static readonly DependencyProperty ForegroundProperty = DependencyProperty.Register(
            nameof(Foreground),
            typeof(Color?),
            typeof(ToolbarIconPresenter),
            new PropertyMetadata(null, OnForegroundChanged));

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            RegisterForXamlRootChanges();
            RefreshIcon();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            AttachButton(null);
            UnregisterFromXamlRootChanges();
        }

        private static void OnButtonChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var presenter = (ToolbarIconPresenter)d;
            presenter.AttachButton(e.NewValue as ToolbarButton);
            presenter.RefreshIcon();
        }

        private static void OnIconSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ToolbarIconPresenter)d).RefreshIcon();
        }

        private static void OnForegroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ToolbarIconPresenter)d).RefreshIcon();
        }

        private void AttachButton(ToolbarButton button)
        {
            if (_button != null)
            {
                _button.PropertyChanged -= OnButtonPropertyChanged;
            }

            _button = button;

            if (_button != null)
            {
                _button.PropertyChanged += OnButtonPropertyChanged;
            }
        }

        private void OnButtonPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e == null)
            {
                RequestRefreshIcon();
                return;
            }

            if (e.PropertyName == nameof(ToolbarButton.IconType) ||
                e.PropertyName == nameof(ToolbarButton.IconPath) ||
                e.PropertyName == nameof(ToolbarButton.IconGlyph))
            {
                RequestRefreshIcon();
            }
        }

        private void RequestRefreshIcon()
        {
            var queue = DispatcherQueue;
            if (queue != null && !queue.HasThreadAccess)
            {
                queue.TryEnqueue(RefreshIcon);
            }
            else
            {
                RefreshIcon();
            }
        }

        private void RefreshIcon()
        {
            // Ensure we operate on UI thread
            var queue = DispatcherQueue;
            if (queue != null && !queue.HasThreadAccess)
            {
                queue.TryEnqueue(RefreshIcon);
                return;
            }

            RegisterForXamlRootChanges();

            double size = IconSize;
            if (double.IsNaN(size) || size <= 0)
            {
                size = 32d;
            }

            Width = size;
            Height = size;

            double scale = 1.0;
            if (_xamlRoot != null)
            {
                scale = _xamlRoot.RasterizationScale;
            }

            var iconElement = IconElementFactory.Create(_button, size, scale, Foreground);
            if (iconElement != null)
            {
                iconElement.HorizontalAlignment = HorizontalAlignment.Center;
                iconElement.VerticalAlignment = VerticalAlignment.Center;
                iconElement.Width = size;
                iconElement.Height = size;
            }

            Children.Clear();
            if (iconElement != null)
            {
                Children.Add(iconElement);
            }
        }

        private void RegisterForXamlRootChanges()
        {
            var root = XamlRoot;
            if (root == null || ReferenceEquals(root, _xamlRoot))
            {
                return;
            }

            UnregisterFromXamlRootChanges();
            _xamlRoot = root;
            _xamlRoot.Changed += OnXamlRootChanged;
        }

        private void UnregisterFromXamlRootChanges()
        {
            if (_xamlRoot != null)
            {
                _xamlRoot.Changed -= OnXamlRootChanged;
                _xamlRoot = null;
            }
        }

        private void OnXamlRootChanged(XamlRoot sender, object args)
        {
            RequestRefreshIcon();
        }
    }
}
