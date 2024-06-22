// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AdvancedPaste.Controls
{
    [TemplatePart(Name = LoadingGrid, Type = typeof(Grid))]
    [TemplatePart(Name = LoadingBrush, Type = typeof(AnimatedBorderBrush))]
    public class AnimatedContentControl : ContentControl
    {
        internal const string LoadingGrid = "PART_LoadingGrid";
        internal const string LoadingBrush = "PART_LoadingBrush";

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(AnimatedContentControl),
        new PropertyMetadata(defaultValue: null));

        public bool IsLoading
        {
            get => (bool)GetValue(IsLoadingProperty);
            set => SetValue(IsLoadingProperty, value);
        }

        public static readonly DependencyProperty IsLoadingProperty = DependencyProperty.Register(
        nameof(IsLoading),
        typeof(bool),
        typeof(AnimatedContentControl),
        new PropertyMetadata(defaultValue: false, (d, e) => ((AnimatedContentControl)d).OnIsLoadingPropertyChanged((bool)e.OldValue, (bool)e.NewValue)));

        public AnimatedContentControl()
        {
            this.DefaultStyleKey = typeof(AnimatedContentControl);
        }

        protected override void OnApplyTemplate()
        {
            this.SizeChanged -= AICard_SizeChanged;
            OnIsLoadingChanged();
            UpdateSize(Width, Height);
            this.SizeChanged += AICard_SizeChanged;
        }

        private void AICard_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (GetTemplateChild(LoadingBrush) is AnimatedBorderBrush loadingBrush)
            {
                    UpdateSize(e.NewSize.Width, e.NewSize.Height);
            }
        }

        private void UpdateSize(double width, double height)
        {
            if (GetTemplateChild(LoadingBrush) is AnimatedBorderBrush loadingBrush)
            {
                loadingBrush.UpdateSize(width, height);
            }
        }

        protected virtual void OnIsLoadingPropertyChanged(bool oldValue, bool newValue)
        {
            OnIsLoadingChanged();
        }

        private void OnIsLoadingChanged()
        {
            if (GetTemplateChild(LoadingBrush) is AnimatedBorderBrush loadingBrush)
            {
                UpdateSize(ActualWidth, ActualHeight);

                loadingBrush.IsLoading = IsLoading;
            }

            if (GetTemplateChild(LoadingGrid) is Grid loadingGrid)
            {
                loadingGrid.Visibility = IsLoading ? Visibility.Visible : Visibility.Collapsed;
                UpdateSize(ActualWidth, ActualHeight);
            }
        }
    }
}
