// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Helpers;

/// <summary>
/// Preserves types required by XAML for Native AOT compilation.
/// Called from App constructor when BUILD_INFO_PUBLISH_AOT is defined.
/// </summary>
internal static class TypePreservation
{
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Controls.FontIconSource))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Controls.PathIcon))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Controls.SymbolIcon))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Controls.SymbolIconSource))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Controls.ImageIcon))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Controls.BitmapIcon))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.DataTemplate))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Controls.DataTemplateSelector))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Controls.NavigationView))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Controls.NavigationViewItem))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Controls.Frame))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Controls.Page))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Controls.ContentControl))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Controls.ListView))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Controls.ListViewItem))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Controls.Grid))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Controls.StackPanel))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Controls.Border))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Controls.TextBlock))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Controls.TextBox))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Controls.Button))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Controls.ComboBox))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Controls.CheckBox))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Controls.ToggleSwitch))]
    public static void PreserveTypes()
    {
        // This method exists only to hold DynamicDependency attributes.
        // Called from App constructor to ensure types aren't trimmed during AOT compilation.
    }
}
