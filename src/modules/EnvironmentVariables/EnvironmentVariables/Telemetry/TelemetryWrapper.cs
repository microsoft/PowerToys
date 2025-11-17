// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using EnvironmentVariablesUILib.Models;
using EnvironmentVariablesUILib.Telemetry;
using Microsoft.PowerToys.Telemetry;

namespace EnvironmentVariables.Telemetry
{
    internal sealed class TelemetryWrapper : ITelemetry
    {
        public void LogEnvironmentVariablesProfileEnabledEvent(bool enabled)
        {
            var telemetryEnabled = new EnvironmentVariablesProfileEnabledEvent()
            {
                Enabled = enabled,
            };

            PowerToysTelemetry.Log.WriteEvent(telemetryEnabled);
        }

        public void LogEnvironmentVariablesVariableChangedEvent(VariablesSetType type)
        {
            PowerToysTelemetry.Log.WriteEvent(new Telemetry.EnvironmentVariablesVariableChangedEvent(type));
        }
    }
}
