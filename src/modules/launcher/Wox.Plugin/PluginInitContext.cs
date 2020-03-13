using System;

namespace Wox.Plugin
{
    public class PluginInitContext
    {
        public PluginMetadata CurrentPluginMetadata { get; internal set; }

        /// <summary>
        /// Public APIs for plugin invocation
        /// </summary>
        public IPublicAPI API { get; set; }
    }
}
