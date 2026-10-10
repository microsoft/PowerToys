// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common;

namespace Microsoft.CmdPal.UI.Helpers;

/// <summary>Captures the system contrast qualifier and palette for one icon request.</summary>
public readonly record struct IconContrast(IconContrastMode Mode, uint Foreground = 0, uint Background = 0)
{
    public bool IsHighContrast => Mode != IconContrastMode.Standard;

    /// <summary>Uses the captured system palette, with defaults for callers supplying only a qualifier.</summary>
    internal uint ForegroundArgb => Foreground != 0 ? Foreground
        : Mode == IconContrastMode.White ? 0xFF000000 : 0xFFFFFFFF;

    internal uint BackgroundArgb => Background != 0 ? Background
        : Mode == IconContrastMode.White ? 0xFFFFFFFF : 0xFF000000;
}
