// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Settings;

/// <summary>
/// How the dashboard covers the desktop behind it.
/// </summary>
public enum DashboardBackdrop
{
    /// <summary>
    /// Dims the desktop with a translucent scrim. Fades in smoothly.
    /// </summary>
    Dim,

    /// <summary>
    /// Blurs the desktop with acrylic, then dims it.
    /// </summary>
    Acrylic,
}
