// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.Common.Services;
using Microsoft.CmdPal.UI.ViewModels.Services;

namespace Microsoft.CmdPal.UI.Services;

/// <summary>
/// Provides theme identification, resource path resolution, and creation of acrylic
/// backdrop parameters based on the current <see cref="ThemeContext"/>.
/// </summary>
/// <remarks>
/// Implementations should expose a stable <see cref="ThemeKey"/> and a valid XAML resource
/// dictionary path via <see cref="ResourcePath"/>. The
/// <see cref="GetAcrylicBackdrop(ThemeContext)"/> method computes
/// <see cref="AcrylicBackdropParameters"/> using the supplied theme context.
/// </remarks>
internal interface IThemeProvider
{
    /// <summary>
    /// Gets the unique key identifying this theme provider.
    /// </summary>
    string ThemeKey { get; }

    /// <summary>
    /// Gets the resource dictionary path for this theme.
    /// </summary>
    string ResourcePath { get; }

    /// <summary>
    /// Creates acrylic backdrop parameters based on the provided theme context.
    /// </summary>
    /// <param name="context">The current theme context, including theme, tint, and optional background details.</param>
    /// <returns>The computed <see cref="AcrylicBackdropParameters"/> for the backdrop.</returns>
    AcrylicBackdropParameters GetAcrylicBackdrop(ThemeContext context);
}
