namespace Wox.Plugin.WebSearch.SuggestionSources
{
    public class SuggestionSourceFactory
    {
        public static ISuggestionSource GetSuggestionSource(string name,PluginInitContext context)
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
