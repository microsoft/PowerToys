// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class PowerLauncherPluginSettings
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public string Author { get; set; }

        public bool Disabled { get; set; }

        public bool IsGlobal { get; set; }

        public string ActionKeyword { get; set; }

        public string IconPathDark { get; set; }

        public string IconPathLight { get; set; }

        public IEnumerable<PluginAdditionalOption> AdditionalOptions { get; set; }
    }
}
