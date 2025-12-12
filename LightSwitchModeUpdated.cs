// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using Microsoft.PowerToys.Telemetry;
using Microsoft.PowerToys.Telemetry.Events;

/// <summary>
/// Tracks which mode users are using to schedule theme changes
/// Purpose: Identify which modes users are using the most.
/// </summary>
[EventData]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
public class LightSwitchModeUpdated : EventBase, IEvent
{
  /// <summary>
  /// Gets or sets the mode the user is using.
  /// </summary>
  public string Mode { get; set; }

  public LightSwitchModeUpdated(bool mode)
  {
    EventName = "LightSwitch_ModeUpdated";
    Mode = mode;
  }

  public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;
}
