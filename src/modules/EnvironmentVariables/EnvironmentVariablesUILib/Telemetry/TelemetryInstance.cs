// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using EnvironmentVariablesUILib.Telemetry;

namespace EnvironmentVariablesUILib.Helpers
{
    public static class TelemetryInstance
    {
        public static ITelemetry Telemetry { get; set; }
    }
}
