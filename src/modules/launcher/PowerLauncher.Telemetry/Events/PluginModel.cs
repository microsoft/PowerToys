// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerLauncher.Telemetry.Events
{
    public class PluginModel
    {
        public string Name { get; set; }

        public bool Disabled { get; set; }

        public bool IsGlobal { get; set; }

        public string ActionKeyword { get; set; }
    }
}
