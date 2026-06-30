// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerDisplay.Cli.Commands;

public static class AdjustCommand
{
    /// <summary>
    /// Counts how many continuous-setting flags are set in <paramref name="inputs"/>.
    /// Exactly one must be true for a valid <c>up</c>/<c>down</c> invocation.
    /// </summary>
    public static int CountSelectedSettings(AdjustCommandInputs inputs)
    {
        var count = 0;
        if (inputs.Brightness)
        {
            count++;
        }

        if (inputs.Contrast)
        {
            count++;
        }

        if (inputs.Volume)
        {
            count++;
        }

        return count;
    }
}
