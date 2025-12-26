// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;

namespace PowerDisplay.Helpers
{
    /// <summary>
    /// This class ensures types used in XAML are preserved during AOT compilation.
    /// Framework types cannot have attributes added directly to their definitions since they're external types.
    /// Use DynamicDependency to preserve all members of these WinUI3 framework types.
    /// </summary>
    internal static class TypePreservation
    {
        // Core WinUI3 Controls used in XAML
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Window))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Application))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Controls.Grid))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Controls.Border))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Controls.ScrollViewer))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Controls.StackPanel))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Controls.ItemsControl))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Controls.Slider))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Controls.TextBlock))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Controls.Button))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Controls.FontIcon))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Controls.ProgressRing))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Controls.InfoBar))]

        // Animation and Transform types
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Media.Animation.Storyboard))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Media.Animation.DoubleAnimation))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Media.Animation.CubicEase))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Media.TranslateTransform))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Media.Animation.TransitionCollection))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Media.Animation.EntranceThemeTransition))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Media.Animation.RepositionThemeTransition))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Media.Animation.EasingFunctionBase))]

        // Template and Resource types
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.DataTemplate))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Controls.ItemsPanelTemplate))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Style))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Controls.FontIconSource))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.ResourceDictionary))]

        // Text and Document types
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Documents.Run))]

        // Layout types
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Controls.RowDefinition))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Controls.ColumnDefinition))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Controls.RowDefinitionCollection))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Controls.ColumnDefinitionCollection))]

        // Media types for brushes and visuals
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Media.SolidColorBrush))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Media.Brush))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Media.Transform))]

        // Core UI element types
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.UIElement))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.FrameworkElement))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.DependencyObject))]

        // Thickness and other value types used in XAML (structs, not enums)
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Thickness))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.CornerRadius))]
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.GridLength))]

        // ToolTip service used in buttons
        [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Controls.ToolTipService))]

        public static void PreserveTypes()
        {
            // This method exists only to hold the DynamicDependency attributes above.
            // It must be called to ensure the types are not trimmed during AOT compilation.
        }
    }
}
