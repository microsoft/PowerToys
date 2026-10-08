// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace CoreWidgetProvider.Helpers;

/// <summary>
/// Maintains fixed-length sample histories. The cards draw them with the Adaptive Cards
/// <c>Chart.Line</c> element, which Command Palette renders natively.
/// </summary>
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
