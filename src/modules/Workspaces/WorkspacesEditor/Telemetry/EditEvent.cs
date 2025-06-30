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

        // True if operation successfully completely. False if failed.
        public bool Successful { get; set; }

        // Change in number of screens in project
        public int ScreenCountDelta { get; set; }

        // Number of apps added to project through editing
        public int AppsAdded { get; set; }

        // Number of apps removed from project through editing
        public int AppsRemoved { get; set; }

        // Number of apps with CLI args added
        public int CliAdded { get; set; }

        // Number of apps with CLI args removed
        public int CliRemoved { get; set; }

        // Number of apps with admin added
        public int AdminAdded { get; set; }

        // Number of apps with admin removed
        public int AdminRemoved { get; set; }

        // True if used window pixel sizing boxes to adjust size
        public bool PixelAdjustmentsUsed { get; set; }

        public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;
    }
}
