using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wox.Core.Exception
{
    public class WoxPluginException : WoxException
    {
        public string PluginName { get; set; }

        public WoxPluginException(string pluginName,System.Exception e)
            : base(e.Message,e)
        {
            PluginName = pluginName;
        }
    }
}
