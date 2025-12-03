// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Telemetry;
using Microsoft.PowerToys.Telemetry.Events;

namespace Microsoft.CommandPalette.UI.Services.Telemetry;

public abstract class TelemetryEventBase : EventBase, IEvent
{
    // Overridden in derived classes
    public abstract PartA_PrivTags PartA_PrivTags { get; }
}
