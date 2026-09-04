// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Numerics;
using System.Threading.Tasks;
using CommunityToolkit.WinUI.Animations;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.UI.ViewManagement;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public sealed partial class UpdateActivityControl : UserControl
    {
        private readonly UISettings _uiSettings = new();
        private int _visibilityAnimationVersion;

        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register(
                nameof(ViewModel),
                typeof(UpdateViewModel),
                typeof(UpdateActivityControl),
                new PropertyMetadata(null));

        public static readonly DependencyProperty IsActivityVisibleProperty =
            DependencyProperty.Register(
                nameof(IsActivityVisible),
                typeof(bool),
                typeof(UpdateActivityControl),
                new PropertyMetadata(false, OnIsActivityVisibleChanged));

        public UpdateViewModel ViewModel
        {
            get => (UpdateViewModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        public bool IsActivityVisible
        {
            get => (bool)GetValue(IsActivityVisibleProperty);
            set => SetValue(IsActivityVisibleProperty, value);
        }

        public UpdateActivityControl()
        {
            InitializeComponent();
            Unloaded += UpdateActivityControl_Unloaded;
        }

        public void Open()
        {
            ViewModel?.RequestActivity();
        }

        private static void OnIsActivityVisibleChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
        {
            var control = (UpdateActivityControl)dependencyObject;
            _ = control.UpdateActivityVisibilityAsync((bool)args.NewValue);
        }

        private async Task UpdateActivityVisibilityAsync(bool isVisible)
        {
            var animationVersion = ++_visibilityAnimationVersion;

            if (!_uiSettings.AnimationsEnabled)
            {
                ActivitySurface.Opacity = isVisible ? 1 : 0;
                ActivitySurface.Translation = new Vector3(0, isVisible ? 0 : 12, 12);
                ActivitySurface.IsHitTestVisible = isVisible;
                ActivitySurface.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
                return;
            }

            try
            {
                if (isVisible)
                {
                    ActivitySurface.IsHitTestVisible = true;
                    ActivitySurface.Visibility = Visibility.Visible;
                    await ((AnimationSet)Resources["ShowActivityAnimation"]).StartAsync(ActivitySurface);
                }
                else if (ActivitySurface.Visibility == Visibility.Visible)
                {
                    ActivitySurface.IsHitTestVisible = false;
                    await ((AnimationSet)Resources["HideActivityAnimation"]).StartAsync(ActivitySurface);

                    if (animationVersion == _visibilityAnimationVersion)
                    {
                        ActivitySurface.Visibility = Visibility.Collapsed;
                    }
                }
            }
            catch (OperationCanceledException) when (animationVersion != _visibilityAnimationVersion)
            {
            }
        }

        private void UpdateActivityControl_Unloaded(object sender, RoutedEventArgs e)
        {
            ++_visibilityAnimationVersion;
        }
    }
}
