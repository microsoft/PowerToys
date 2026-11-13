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
    public class EditEvent : EventBase, IEvent
    {
        public EditEvent()
        {
            EventName = "Workspaces_EditEvent";
        }

        public bool Successful { get; set; }

        public int ScreenCountDelta { get; set; }

        public int AppsAdded { get; set; }

        public int AppsRemoved { get; set; }

        public int CliAdded { get; set; }

        public int CliRemoved { get; set; }

        public int AdminAdded { get; set; }

        public int AdminRemoved { get; set; }

        public bool PixelAdjustmentsUsed { get; set; }

        public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;
    }
}
