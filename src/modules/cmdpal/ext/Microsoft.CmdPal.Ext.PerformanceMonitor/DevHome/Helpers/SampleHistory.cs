// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading;
using Microsoft.CmdPal.Ext.PerformanceMonitor;

namespace CoreWidgetProvider.Helpers;

/// <summary>A fixed-length, thread-safe history of one-second samples, oldest first.</summary>
internal sealed class SampleHistory
{
    private readonly Lock _lock = new();
    private readonly List<float> _values = [];

    public void Add(float value)
    {
        lock (_lock)
        {
            ChartHelper.AddNextChartValue(value, _values, PerformanceChartData.HistoryLength);
        }
    }

    public float[] Snapshot()
    {
        lock (_lock)
        {
            return _values.ToArray();
        }
    }
}
