// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics.Tracing;
using Microsoft.PowerToys.Telemetry;
using Microsoft.PowerToys.Telemetry.Events;

namespace PowerLauncher.Telemetry.Events
{
    [EventData]
    public class RunPluginsSettingsEvent : EventBase, IEvent
    {
        public RunPluginsSettingsEvent(IDictionary<string, PluginModel> pluginManager)
        {
            PluginManager = pluginManager;
        }

        public IDictionary<string, PluginModel> PluginManager { get; private set; }

        public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;
    }
}
