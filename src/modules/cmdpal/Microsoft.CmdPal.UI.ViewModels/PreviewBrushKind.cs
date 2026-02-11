// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// Specifies the brush type to use for backdrop preview approximation.
/// </summary>
public enum PreviewBrushKind
{
    /// <summary>
    /// SolidColorBrush with computed alpha.
    /// </summary>
    Solid,

    /// <summary>
    /// AcrylicBrush with blur effect.
    /// </summary>
    Acrylic,
}
