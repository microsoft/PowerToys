// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerDisplay.Models;

/// <summary>
/// Defines when the built-in display uses the power-saving refresh rate.
/// Values match the Settings dropdown order.
/// </summary>
public enum BatteryRefreshRateMode
{
    /// <summary>Disables automatic refresh-rate changes.</summary>
    Off = 0,

    /// <summary>Lowers the refresh rate while running on battery.</summary>
    OnBattery = 1,

    /// <summary>Lowers the refresh rate while Energy Saver (Battery Saver on Windows 10) is active.</summary>
    OnEnergySaverMode = 2,

    /// <summary>Lowers the refresh rate only while both battery power and Energy Saver are active.</summary>
    OnBatteryAndEnergySaver = 3,
}
