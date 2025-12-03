// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Telemetry;

namespace Microsoft.CommandPalette.UI.Services.Telemetry;

internal static class TelemetryService
{
    public static void WriteEvent(TelemetryEventBase telemetryEvent) => PowerToysTelemetry.Log.WriteEvent(telemetryEvent);
}
