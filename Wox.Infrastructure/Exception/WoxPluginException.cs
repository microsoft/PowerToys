namespace Wox.Core.Exception
{
    public class WoxPluginException : WoxException
    {
        public string PluginName { get; set; }

        public WoxPluginException(string pluginName, string msg, System.Exception e)
            : base($"{msg}: {pluginName}", e)
        {
            PluginName = pluginName;
        }
    }
}
