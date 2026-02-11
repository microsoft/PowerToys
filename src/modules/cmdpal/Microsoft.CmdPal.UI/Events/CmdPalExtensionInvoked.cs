// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using Microsoft.PowerToys.Telemetry;
using Microsoft.PowerToys.Telemetry.Events;

namespace Microsoft.CmdPal.UI.Events;

/// <summary>
/// Tracks extension usage with extension name and invocation details.
/// Purpose: Identify popular vs. unused plugins and track extension performance.
/// </summary>
[EventData]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
public class CmdPalExtensionInvoked : EventBase, IEvent
{
    /// <summary>
    /// Gets or sets the unique identifier of the extension provider.
    /// </summary>
    public string ExtensionId { get; set; }

    /// <summary>
    /// Gets or sets the non-localized identifier of the command being invoked.
    /// </summary>
    public string CommandId { get; set; }

    /// <summary>
    /// Gets or sets the localized display name of the command being invoked.
    /// </summary>
    public string CommandName { get; set; }

    /// <summary>
    /// Gets or sets whether the command executed successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the execution time in milliseconds.
    /// </summary>
    public ulong ExecutionTimeMs { get; set; }

    public CmdPalExtensionInvoked(string extensionId, string commandId, string commandName, bool success, ulong executionTimeMs)
    {
        EventName = "CmdPal_ExtensionInvoked";
        ExtensionId = extensionId;
        CommandId = commandId;
        CommandName = commandName;
        Success = success;
        ExecutionTimeMs = executionTimeMs;
    }

    public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;
}
