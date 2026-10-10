// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

namespace CoreWidgetProvider.Helpers;

internal sealed partial class TemperatureStats : PerformanceCounterSourceBase, IDisposable
{
    private const string CategoryName = "Thermal Zone Information";
    private const string CounterName = "High Precision Temperature";

    private const double TenthsKelvinOffset = 2731.5;

    private const double MinPlausibleCelsius = -20.0;
    private const double MaxPlausibleCelsius = 150.0;

    private readonly PerformanceCounter? _thermalCounter;
    private bool _readFailureLogged;

    public bool IsAvailable => _thermalCounter is not null;

    public double? TemperatureCelsius { get; private set; }

    public TemperatureStats()
    {
        try
        {
            var category = CreatePerformanceCounterCategory(CategoryName, logFailure: false);
            if (category is null)
            {
                return;
            }

            var instances = category.GetInstanceNames();
            if (instances.Length == 0)
            {
                return;
            }

            var preferred = Array.Find(instances, n => n.StartsWith("_TZ.", StringComparison.OrdinalIgnoreCase))
                ?? instances[0];

            _thermalCounter = CreatePerformanceCounter(CategoryName, CounterName, preferred, logFailure: false);
        }
        catch (Exception ex)
        {
            LogFailureOnce(ref _readFailureLogged, $"Failed to initialize {CategoryName} performance counter.", ex);
        }
    }

    public void GetData()
    {
        if (_thermalCounter is null)
        {
            TemperatureCelsius = null;
            return;
        }

        try
        {
            var raw = _thermalCounter.NextValue();
            var celsius = (raw - TenthsKelvinOffset) / 10.0;

            TemperatureCelsius = celsius >= MinPlausibleCelsius && celsius <= MaxPlausibleCelsius
                ? celsius
                : null;
        }
        catch (Exception ex)
        {
            LogFailureOnce(ref _readFailureLogged, $"Failed to read {CategoryName}\\{CounterName}.", ex);
            TemperatureCelsius = null;
        }
    }

    public void Dispose()
    {
        _thermalCounter?.Dispose();
    }
}
