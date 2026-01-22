// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Microsoft.CmdPal.UI.Services;

internal sealed record ThemeContext
{
    public ElementTheme Theme { get; init; }

    public Color Tint { get; init; }

    public ImageSource? BackgroundImageSource { get; init; }

    public Stretch BackgroundImageStretch { get; init; }

    public double BackgroundImageOpacity { get; init; }

    public int? ColorIntensity { get; init; }
}
