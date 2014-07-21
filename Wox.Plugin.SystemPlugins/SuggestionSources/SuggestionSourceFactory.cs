using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wox.Plugin.SystemPlugins.SuggestionSources
{
    public class SuggestionSourceFactory
    {
        public static ISuggestionSource GetSuggestionSource(string name)
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
