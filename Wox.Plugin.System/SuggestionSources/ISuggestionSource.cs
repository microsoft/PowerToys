using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Wox.Plugin.System.SuggestionSources
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
