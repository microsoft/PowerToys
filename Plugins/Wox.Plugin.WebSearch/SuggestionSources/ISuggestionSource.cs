using System.Collections.Generic;

namespace Wox.Plugin.WebSearch.SuggestionSources
{
    public abstract class SuggestionSource
    {
        public virtual string Domain { get; set; }
        public IHttpProxy Proxy { get; set; }

        public SuggestionSource(IHttpProxy httpProxy)
        {
            Proxy = httpProxy;
        }

        public abstract List<string> GetSuggestions(string query);

        public static SuggestionSource GetSuggestionSource(string name, PluginInitContext context)
        {
            switch (name.ToLower())
            {
                case "google":
                    return new Google(context.Proxy);

                case "baidu":
                    return new Baidu(context.Proxy);

                default:
                    return null;
            }
        }
    }
}
