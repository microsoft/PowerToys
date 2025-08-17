﻿// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using Microsoft.PowerToys.Telemetry;
using Microsoft.PowerToys.Telemetry.Events;

namespace Microsoft.PowerLauncher.Telemetry
{
    /// <summary>
    /// ETW event for when a result is actioned.
    /// </summary>
    [EventData]
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
    public class LauncherResultActionEvent : EventBase, IEvent
    {
        public enum TriggerType
        {
            Click,
            KeyboardShortcut,
        }

        public string Trigger { get; set; }

        public string PluginName { get; set; }

        public string ActionName { get; set; }

        public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;
    }
}
