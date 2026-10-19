// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using Microsoft.PowerToys.Telemetry;
using Microsoft.PowerToys.Telemetry.Events;

namespace PowerDisplay.Cli.Telemetry;

/// <summary>
/// Telemetry event for PowerDisplay CLI command execution.
/// </summary>
[EventData]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
public class PowerDisplayCLICommandEvent : EventBase, IEvent
{
    public PowerDisplayCLICommandEvent()
    {
        EventName = "PowerDisplay_CLICommand";
        CommandName = string.Empty;
    }

    public string CommandName { get; set; }

    public bool Successful { get; set; }

    public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;
}
