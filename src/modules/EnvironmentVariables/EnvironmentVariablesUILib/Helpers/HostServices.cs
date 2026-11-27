// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using EnvironmentVariablesUILib.Models;
using EnvironmentVariablesUILib.Telemetry;

namespace EnvironmentVariablesUILib.Helpers
{
    internal static class HostServices
    {
        internal static void Initialize(IElevationHelper elevationHelper, ILogger logger, ITelemetry telemetry)
        {
            ElevationHelper.ElevationHelperInstance = elevationHelper ?? new ElevationHelper();
            LoggerInstance.Logger = logger ?? new NoOpLogger();
            TelemetryInstance.Telemetry = telemetry ?? new NoOpTelemetry();
        }

        private sealed class NoOpLogger : ILogger
        {
            public void LogDebug(string message)
            {
            }

            public void LogError(string message)
            {
            }

            public void LogError(string message, Exception ex)
            {
            }

            public void LogInfo(string message)
            {
            }

            public void LogTrace()
            {
            }

            public void LogWarning(string message)
            {
            }
        }

        private sealed class NoOpTelemetry : ITelemetry
        {
            public void LogEnvironmentVariablesProfileEnabledEvent(bool enabled)
            {
            }

            public void LogEnvironmentVariablesVariableChangedEvent(VariablesSetType type)
            {
            }
        }
    }
}
