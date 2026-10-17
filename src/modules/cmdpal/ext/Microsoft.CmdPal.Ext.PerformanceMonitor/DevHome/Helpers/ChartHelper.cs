// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace CoreWidgetProvider.Helpers;

/// <summary>Maintains the fixed-length sample histories that the cards' line charts show.</summary>
internal sealed class ChartHelper
{
    public static void AddNextChartValue(float value, List<float> chartValues, int maxValues)
    {
        while (chartValues.Count >= maxValues)
        {
            chartValues.RemoveAt(0);
        }

        chartValues.Add(value);
    }
}
