// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;

namespace Microsoft.CmdPal.UI.Helpers;

/// <summary>Captures the surface theme and system contrast before icon work is queued.</summary>
/// <param name="Theme">The rendering surface's theme.</param>
/// <param name="Contrast">The captured Windows contrast qualifier and colors.</param>
public readonly record struct IconRenderContext(ElementTheme Theme, IconContrast Contrast);
