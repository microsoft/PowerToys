// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.UI;

/// <summary>
/// This class ensures types used in XAML are preserved during AOT compilation.
/// Framework types cannot have attributes added directly to their definitions since they're external types.
/// Application types that require runtime type checking should also be preserved here if needed.
/// </summary>
internal static class TypePreservation
{
    /// <summary>
    /// This method ensures critical types are preserved for AOT compilation.
    /// These types are used dynamically in XAML and would otherwise be trimmed.
    /// </summary>
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Controls.FontIconSource))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Controls.PathIcon))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.DataTemplate))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Controls.DataTemplateSelector))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Microsoft.UI.Xaml.Controls.ListViewItem))]
    public static void PreserveTypes()
    {
        // This method exists only to hold the DynamicDependency attributes above.
        // It must be called to ensure the types are not trimmed during AOT compilation.

        // Note: We cannot add [DynamicallyAccessedMembers] directly to framework types
        // since we don't own their source code. DynamicDependency is the correct approach
        // for preserving external types that are used dynamically (e.g., in XAML).

        // For application types that require runtime type checking (e.g., in template selectors),
        // prefer adding [DynamicallyAccessedMembers] attributes directly on the type definitions.
        // Only use DynamicDependency here for types we cannot modify directly.
    }
}
