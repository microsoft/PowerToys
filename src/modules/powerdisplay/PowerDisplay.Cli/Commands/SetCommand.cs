// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerDisplay.Cli.Commands;

public static class SetCommand
{
    /// <summary>
    /// Counts how many settings are specified in <paramref name="inputs"/>.
    /// Exactly one must be non-null/non-zero for a valid <c>set</c> invocation.
    /// </summary>
    public static int CountSelectedSettings(SetCommandInputs inputs)
    {
        int count = 0;
        if (inputs.Brightness.HasValue)
        {
            count++;
        }

        if (inputs.Contrast.HasValue)
        {
            count++;
        }

        if (inputs.Volume.HasValue)
        {
            count++;
        }

        if (inputs.ColorTemperature is not null)
        {
            count++;
        }

        if (inputs.InputSource is not null)
        {
            count++;
        }

        if (inputs.PowerState is not null)
        {
            count++;
        }

        if (inputs.Orientation is not null)
        {
            count++;
        }

        return count;
    }
}
