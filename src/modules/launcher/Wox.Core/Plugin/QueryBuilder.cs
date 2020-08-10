// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Wox.Plugin;

namespace Wox.Core.Plugin
{
    public static class QueryBuilder
    {
        public static Dictionary<PluginPair, Query> Build(ref string text, Dictionary<string, PluginPair> nonGlobalPlugins)
        {
            // replace multiple white spaces with one white space
            var terms = text.Split(new[] { Query.TermSeparator }, StringSplitOptions.RemoveEmptyEntries);
            if (terms.Length == 0)
            { // nothing was typed
                return null;
            }

            // This Dictionary contains the corresponding query for each plugin
            Dictionary<PluginPair, Query> pluginQueryPair = new Dictionary<PluginPair, Query>();

            var rawQuery = string.Join(Query.TermSeparator, terms);

            // This is the query on removing extra spaces which would be executed by global Plugins
            text = rawQuery;

            string possibleActionKeyword = terms[0];

            foreach(PluginPair nonGlobalPlugin in nonGlobalPlugins.Values)
            {
                string pluginActionKeyword = nonGlobalPlugin.Metadata.ActionKeyword;
                if (possibleActionKeyword.StartsWith(pluginActionKeyword))
                {
                    // The search string is the raw query excluding the action keyword
                    string search = rawQuery.Length > pluginActionKeyword.Length ? rawQuery.Substring(pluginActionKeyword.Length).Trim() : string.Empty;
                    
                    // A new query is constructed for each plugin as they have different action keywords
                    var query = new Query(rawQuery, search, terms, pluginActionKeyword);

                    pluginQueryPair.Add(nonGlobalPlugin, query);
                }
            }

            foreach(PluginPair globalPlugin in PluginManager.GlobalPlugins)
            {
                var query = new Query(rawQuery, rawQuery, terms, String.Empty);
                pluginQueryPair.Add(globalPlugin, query);
            }

            return pluginQueryPair;
        }
    }
}
