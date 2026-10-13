// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;

using Microsoft.PowerToys.Telemetry;
using Microsoft.PowerToys.Telemetry.Events;

namespace AdvancedPaste.Telemetry;

/// <summary>
/// Telemetry event for Advanced Paste CLI command execution.
/// </summary>
[EventData]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
public class AdvancedPasteCLICommandEvent : EventBase, IEvent
{
    public AdvancedPasteCLICommandEvent()
    {
        EventName = "AdvancedPaste_CLICommand";
        CommandName = string.Empty;
    }

    /// <summary>
    /// Gets or sets the bounded name of the CLI command that was executed.
    /// </summary>
    public string CommandName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the command executed successfully.
    /// </summary>
    public bool Successful { get; set; }

    public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;
}
