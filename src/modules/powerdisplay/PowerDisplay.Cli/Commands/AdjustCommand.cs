// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;

namespace PowerDisplay.Cli.Commands;

public static class AdjustCommand
{
    /// <summary>
    /// Counts how many continuous-setting flags are set in <paramref name="inputs"/>.
    /// Exactly one must be true for a valid <c>up</c>/<c>down</c> invocation.
    /// </summary>
    public static int CountSelectedSettings(AdjustCommandInputs inputs)
    {
        // Mirror SetCommand.CountSelectedSettings: list the candidate flags, then Count the selected.
        bool[] flags = [inputs.Brightness, inputs.Contrast, inputs.Volume];
        return flags.Count(f => f);
    }
}
