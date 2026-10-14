// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;

namespace PowerDisplay.Cli.Commands;

public static class SetCommand
{
    /// <summary>
    /// Counts how many settings are specified in <paramref name="inputs"/>.
    /// Exactly one must be non-null for a valid <c>set</c> invocation.
    /// </summary>
    public static int CountSelectedSettings(SetCommandInputs inputs)
    {
        // A continuous int? of 0 still boxes to a non-null object, so zero-valued
        // settings are counted just like the discrete string settings.
        object?[] settings =
        [
            inputs.Brightness,
            inputs.Contrast,
            inputs.Volume,
            inputs.ColorTemperature,
            inputs.InputSource,
            inputs.PowerState,
            inputs.Orientation,
        ];

        return settings.Count(s => s is not null);
    }
}
