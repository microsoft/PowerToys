// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using Microsoft.PowerToys.Telemetry;
using Microsoft.PowerToys.Telemetry.Events;

namespace Microsoft.CmdPal.UI.Events;

/// <summary>
/// Tracks the dock configuration at startup.
/// Purpose: Understand how users configure their dock - which side, which bands, and whether it's enabled.
/// </summary>
[EventData]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
public class CmdPalDockConfiguration : EventBase, IEvent
{
    /// <summary>
    /// Gets or sets whether the dock is enabled.
    /// </summary>
    public bool IsDockEnabled { get; set; }

    /// <summary>
    /// Gets or sets the dock side (top, bottom, left, right, none).
    /// "none" when the dock is disabled.
    /// </summary>
    public string DockSide { get; set; }

    /// <summary>
    /// Gets or sets the start bands as a newline-delimited list of "{ProviderId}/{CommandId}".
    /// Empty if the dock is disabled.
    /// </summary>
    public string StartBands { get; set; }

    /// <summary>
    /// Gets or sets the center bands as a newline-delimited list of "{ProviderId}/{CommandId}".
    /// Empty if the dock is disabled.
    /// </summary>
    public string CenterBands { get; set; }

    /// <summary>
    /// Gets or sets the end bands as a newline-delimited list of "{ProviderId}/{CommandId}".
    /// Empty if the dock is disabled.
    /// </summary>
    public string EndBands { get; set; }

    public CmdPalDockConfiguration(bool isDockEnabled, string dockSide, string startBands, string centerBands, string endBands)
    {
        EventName = "CmdPal_DockConfiguration";
        IsDockEnabled = isDockEnabled;
        DockSide = dockSide;
        StartBands = startBands;
        CenterBands = centerBands;
        EndBands = endBands;
    }

    public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;
}
