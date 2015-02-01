using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wox.Plugin
{
    public class PluginInitContext
    {
        public PluginMetadata CurrentPluginMetadata { get; internal set; }

        /// <summary>
        /// Public APIs for plugin invocation
        /// </summary>
        public IPublicAPI API { get; set; }

        public IHttpProxy Proxy { get; set; }
    }
}
