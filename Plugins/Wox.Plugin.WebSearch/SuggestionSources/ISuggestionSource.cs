using System.Collections.Generic;
using System.Threading.Tasks;

namespace Wox.Plugin.WebSearch.SuggestionSources
{
    //todo rename file
    public abstract class SuggestionSource
    {
        public abstract Task<List<string>> Suggestions(string query);
    }
}