// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.PowerToys.Telemetry;
using Microsoft.PowerToys.Telemetry.Events;

namespace PowerLauncher.Telemetry.Events
{
    public class RunSettingsEvent : EventBase, IEvent
    {
#pragma warning disable CA2227 // Collection properties should be read only
        public IDictionary<string, PluginModel> PluginManager { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only

        public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;
    }
}
