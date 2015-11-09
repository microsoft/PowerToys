using Wox.Plugin;

namespace Wox.Infrastructure.Exception
{
    public class WoxPluginException : WoxException
    {
        public string PluginName { get; set; }

        public WoxPluginException(string pluginName, string msg, System.Exception e)
            : base($"{pluginName} : {msg}", e)
        {
            PluginName = pluginName;
        }

        public WoxPluginException(string pluginName, string msg) : base(msg)
        {
            PluginName = pluginName;
        }
    }
}
