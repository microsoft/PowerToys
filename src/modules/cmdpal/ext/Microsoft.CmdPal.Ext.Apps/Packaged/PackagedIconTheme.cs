// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common;

namespace Microsoft.CmdPal.Ext.Apps.Packaged;

/// <summary>Specifies the surface theme and independent Windows contrast qualifier for packaged artwork.</summary>
/// <param name="IsLight">Whether the rendering surface uses its light theme.</param>
/// <param name="Contrast">The captured system contrast qualifier.</param>
public readonly record struct PackagedIconTheme(bool IsLight, IconContrastMode Contrast = IconContrastMode.Standard)
{
    internal bool IsHighContrast => Contrast != IconContrastMode.Standard;
}
