// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using Microsoft.PowerToys.Telemetry;
using Microsoft.PowerToys.Telemetry.Events;

namespace WorkspacesEditor.Telemetry
{
    [EventData]
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
    public class CreateEvent : EventBase, IEvent
    {
        public CreateEvent()
        {
            EventName = "Workspaces_CreateEvent";
        }

        public bool Successful { get; set; }

        public int NumScreens { get; set; }

        public int AppCount { get; set; }

        public int CliCount { get; set; }

        public int AdminCount { get; set; }

        public bool ShortcutCreated { get; set; }

        public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;
    }
}
