// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace RobocopyUI.Controls
{
    /// <summary>
    /// Content control that draws an animated gradient border while <see cref="IsLoading"/> is set.
    /// Ported from Advanced Paste so the AI prompt has the same "thinking" treatment in both modules.
    /// </summary>
    [TemplatePart(Name = LoadingGrid, Type = typeof(Grid))]
    [TemplatePart(Name = LoadingBrush, Type = typeof(AnimatedBorderBrush))]
    public partial class AnimatedContentControl : ContentControl
    {
        internal const string LoadingGrid = "PART_LoadingGrid";
        internal const string LoadingBrush = "PART_LoadingBrush";

        public static readonly DependencyProperty IsLoadingProperty = DependencyProperty.Register(
            nameof(IsLoading),
            typeof(bool),
            typeof(AnimatedContentControl),
            new PropertyMetadata(defaultValue: false, (d, e) => ((AnimatedContentControl)d).OnIsLoadingChanged()));

        public bool IsLoading
        {
            get => (bool)GetValue(IsLoadingProperty);
            set => SetValue(IsLoadingProperty, value);
        }

        public AnimatedContentControl()
        {
            DefaultStyleKey = typeof(AnimatedContentControl);
        }

        protected override void OnApplyTemplate()
        {
            SizeChanged -= OnSizeChanged;
            OnIsLoadingChanged();
            UpdateSize(ActualWidth, ActualHeight);
            SizeChanged += OnSizeChanged;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
            => UpdateSize(e.NewSize.Width, e.NewSize.Height);

        private void UpdateSize(double width, double height)
        {
            if (GetTemplateChild(LoadingBrush) is AnimatedBorderBrush loadingBrush)
            {
                loadingBrush.UpdateSize(width, height);
            }
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
