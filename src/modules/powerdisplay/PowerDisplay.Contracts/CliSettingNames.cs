// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerDisplay.Contracts;

/// <summary>
/// Canonical setting names accepted by the CLI (the value of <c>--setting</c> and the
/// per-setting <c>--&lt;name&gt;</c> flags). Shared by the CLI argument layer and the app-side
/// executor/projector so the single list cannot drift between the two sides. The same
/// identifiers appear in <see cref="CliSettingValue.Setting"/> so JSON consumers can
/// switch on them.
/// </summary>
public static class CliSettingNames
{
    public const string Brightness = "brightness";

    public const string Contrast = "contrast";

    public const string Volume = "volume";

    public const string ColorTemperature = "color-temperature";

    public const string InputSource = "input-source";

    public const string PowerState = "power-state";

    public const string Orientation = "orientation";

    /// <summary>All canonical setting names, in canonical (display) order.</summary>
    public static readonly string[] All =
    [
        Brightness,
        Contrast,
        Volume,
        ColorTemperature,
        InputSource,
        PowerState,
        Orientation,
    ];
}
