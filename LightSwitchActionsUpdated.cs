// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using Microsoft.PowerToys.Telemetry;
using Microsoft.PowerToys.Telemetry.Events;

/// <summary>
/// Tracks which actions the users are using to apply theme changes
/// Purpose: Identify which parts of the OS it is important for users to control.
/// </summary>
[EventData]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
public class LightSwitchActionsUpdated : EventBase, IEvent
{
  /// <summary>
  /// Gets or sets whether the user is changing their app theme.
  /// </summary>
  public bool ChangeApps { get; set; }

  /// <summary>
  /// Gets or sets whether the user is changing their system theme.
  /// </summary>
  public bool ChangeSystem { get; set; }

  public LightSwitchActionsUpdated(bool, ChangeApps, bool ChangeSystem)
  {
    EventName = "LightSwitch_ModeUpdated";
    ChangeApps = changeApps;
    ChangeSystem = changeSystem
  }

  public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;
}
