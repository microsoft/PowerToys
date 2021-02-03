// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Run.Plugin
{
    public class PluginInitContext
    {
        public PluginMetadata CurrentPluginMetadata { get; internal set; }

        /// <summary>
        /// Gets or sets public APIs for plugin invocation
        /// </summary>
        public IPublicAPI API { get; set; }
    }
}
