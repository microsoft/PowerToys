// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.Tracing;

namespace PowerLauncher.Telemetry.Events
{
    [EventData]
    public class PluginModel
    {
        public string ID { get; set; }

        public string Name { get; set; }

        public bool Disabled { get; set; }

        public bool IsGlobal { get; set; }

        public string ActionKeyword { get; set; }
    }
}
