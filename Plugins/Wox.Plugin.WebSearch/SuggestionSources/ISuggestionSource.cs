using System.Collections.Generic;

namespace Wox.Plugin.WebSearch.SuggestionSources
{
    public interface ISuggestionSource
    {
        List<string> GetSuggestions(string query);
    }

    public abstract class AbstractSuggestionSource : ISuggestionSource
    {
        public abstract List<string> GetSuggestions(string query);
    }
}
