using System;
using System.Collections.Generic;
using System.Linq;
using Wox.Plugin;

namespace Wox.Core.Plugin
{
    public static class QueryBuilder
    {
        public static Query Build(string text, Dictionary<string, PluginPair> nonGlobalPlugins)
        {
            // replace multiple white spaces with one white space
            var terms = text.Split(new[] { Query.TermSeperater }, StringSplitOptions.RemoveEmptyEntries);
            if (terms.Length == 0)
            { // nothing was typed
                return null;
            }

            var rawQuery = string.Join(Query.TermSeperater, terms);
            string actionKeyword, search;
            string possibleActionKeyword = terms[0];
            List<string> actionParameters;
            if (nonGlobalPlugins.TryGetValue(possibleActionKeyword, out var pluginPair) && !pluginPair.Metadata.Disabled)
            { // use non global plugin for query
                actionKeyword = possibleActionKeyword;
                actionParameters = terms.Skip(1).ToList();
                search = actionParameters.Count > 0 ? rawQuery.Substring(actionKeyword.Length + 1) : string.Empty;
            }
            else
            { // non action keyword
                actionKeyword = string.Empty;
                actionParameters = terms.ToList();
                search = rawQuery;
            }

            var query = new Query
            {
                Terms = terms,
                RawQuery = rawQuery,
                ActionKeyword = actionKeyword,
                Search = search,
                // Obsolete value initialisation
                ActionName = actionKeyword,
                ActionParameters = actionParameters
            };

            return query;
        }
    }
}