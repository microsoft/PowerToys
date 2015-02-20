using System.Collections.Generic;

namespace Wox.Plugin.WebSearch.SuggestionSources
{
    public interface ISuggestionSource
    {
        List<string> GetSuggestions(string query);
    }

    public abstract class AbstractSuggestionSource : ISuggestionSource
    {
        public IHttpProxy Proxy { get; set; }

        public AbstractSuggestionSource(IHttpProxy httpProxy)
        {
            Proxy = httpProxy;
        }

        public abstract List<string> GetSuggestions(string query);
    }
}
