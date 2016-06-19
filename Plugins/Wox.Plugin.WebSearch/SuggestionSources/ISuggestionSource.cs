using System.Collections.Generic;
using System.Threading.Tasks;

namespace Wox.Plugin.WebSearch.SuggestionSources
{
    public abstract class SuggestionSource
    {
        public virtual string Domain { get; set; }

        public abstract Task<List<string>> GetSuggestions(string query);

        public static SuggestionSource GetSuggestionSource(string name)
        {
            switch (name.ToLower())
            {
                case "google":
                    return new Google();

                case "baidu":
                    return new Baidu();

                default:
                    return null;
            }
        }
    }
}
