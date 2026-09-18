// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using Microsoft.PowerToys.Telemetry;
using Microsoft.PowerToys.Telemetry.Events;

namespace Microsoft.PowerToys.AI.Telemetry;

/// <summary>
/// Generic scaffold emitted when AI settings or vault credentials are migrated.
/// </summary>
[EventData]
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
public class AISettingsMigrationEvent : EventBase, IEvent
{
    public string ModuleName { get; set; } = string.Empty;

    public string MigrationKind { get; set; } = string.Empty;

    public bool Succeeded { get; set; }

    public AISettingsMigrationEvent()
    {
    }

    public AISettingsMigrationEvent(string moduleName, string migrationKind, bool succeeded)
    {
        ModuleName = moduleName;
        MigrationKind = migrationKind ?? string.Empty;
        Succeeded = succeeded;
    }

    public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;
}
