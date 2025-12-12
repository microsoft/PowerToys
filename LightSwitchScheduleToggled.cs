// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using Microsoft.PowerToys.Telemetry;
using Microsoft.PowerToys.Telemetry.Events;

/// <summary>
/// Tracks if users are using the schedule to control their themeing.
/// Purpose: Identify how users are using Light Switch to control their theme.
/// </summary>
[EventData]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
public class LightSwitchScheduleToggled : EventBase, IEvent
{
  /// <summary>
  /// Gets or sets whether the schedule is on or off.
  /// </summary>
  public bool OnOrOff { get; set; }

  public LightSwitchScheduleToggled(bool onOrOff)
  {
    EventName = "LightSwitch_ScheduleToggled";
    OnOrOff = onOrOff;
  }

  public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;
}
