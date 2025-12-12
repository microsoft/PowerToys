// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using Microsoft.PowerToys.Telemetry;
using Microsoft.PowerToys.Telemetry.Events;

/// <summary>
/// Tracks shortcut usage and which mode are we toggling from and to.
/// Purpose: Identify how users are using the shortcut in their workflows.
/// </summary>
[EventData]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
public class LightSwitchShortcutInvoked : EventBase, IEvent
{
  /// <summary>
  /// Gets or sets the target the mode the user is switching to
  /// </summary>
  public string TargetMode { get; set; }

  /// <summary>
  /// Gets or sets the time of day the user is using this command
  /// </summary>
  public int MinuteOfDay { get; set; }

  public LightSwitchShortcutInvoked(string targetMode, int minuteOfDay)
  {
    EventName = "LightSwitch_ShortcutInvoked";
    TargetMode = targetMode;
    MinuteOfDay = minuteOfDay;
  }

  public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;
}
