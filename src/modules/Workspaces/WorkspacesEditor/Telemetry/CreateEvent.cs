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

        // True if operation successfully completely. False if failed
        public bool Successful { get; set; }

        // Number of screens present in the project
        public int NumScreens { get; set; }

        // Total number of apps in the project
        public int AppCount { get; set; }

        // Number of apps with CLI args
        public int CliCount { get; set; }

        // Number of apps with "Launch as admin" set
        public int AdminCount { get; set; }

        // True if user checked "Create Shortcut". False if not.
        public bool ShortcutCreated { get; set; }

        public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;
    }
}
