// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerDisplay.Ipc;

/// <summary>Whether a VCP setting takes a continuous percentage or a discrete VCP value.</summary>
internal enum CliSettingKind
{
    /// <summary>Percentage value in [0, 100] (brightness, contrast, volume).</summary>
    Continuous,

    /// <summary>Discrete VCP byte chosen from the monitor's advertised set (color-temperature, input-source, power-state).</summary>
    Discrete,
}
