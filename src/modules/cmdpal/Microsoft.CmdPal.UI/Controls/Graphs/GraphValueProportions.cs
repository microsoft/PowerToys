// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Controls.Graphs;

internal static class GraphValueProportions
{
    // Inputs are finite, non-negative values already validated by the host.
    internal static double[] Normalize(ReadOnlySpan<double> values)
    {
        var normalized = new double[values.Length];
        var maximum = 0d;
        foreach (var value in values)
        {
            maximum = Math.Max(maximum, value);
        }

        if (maximum == 0)
        {
            return normalized;
        }

        // Scale before summing so finite inputs cannot overflow the total.
        var total = 0d;
        foreach (var value in values)
        {
            total += value / maximum;
        }

        for (var index = 0; index < values.Length; index++)
        {
            normalized[index] = (values[index] / maximum) / total;
        }

        return normalized;
    }
}
