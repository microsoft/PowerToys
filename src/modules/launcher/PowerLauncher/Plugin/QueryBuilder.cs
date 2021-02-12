// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Collections.Generic;
using Wox.Plugin;

namespace PowerLauncher.Plugin
{
    public static class QueryBuilder
    {
        public static Dictionary<PluginPair, Query> Build(ref string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

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

            foreach (var plugin in PluginManager.NonGlobalPlugins)
            {
                var pluginActionKeyword = plugin.Metadata.ActionKeyword;
                if (plugin.Metadata.Disabled || !possibleActionKeyword.StartsWith(pluginActionKeyword, StringComparison.Ordinal))
                {
                    continue;
                }

                string search = rawQuery.Substring(pluginActionKeyword.Length).Trim();

                // To set the terms of the query after removing the action keyword
                if (possibleActionKeyword.Length > pluginActionKeyword.Length)
                {
                    // If the first term contains the action keyword, then set the remaining string to be the first term
                    terms[0] = possibleActionKeyword.Substring(pluginActionKeyword.Length);
                }
                else
                {
                    // If the first term is the action keyword, then skip it.
                    terms = terms.Skip(1).ToArray();
                }

                // A new query is constructed for each plugin as they have different action keywords
                var query = new Query(rawQuery, search, new ReadOnlyCollection<string>(terms), pluginActionKeyword);

                pluginQueryPair.TryAdd(plugin, query);
            }

            // If the user has specified a matching action keyword, then do not
            // add the global plugins to the list.
            if (pluginQueryPair.Count == 0)
            {
                foreach (PluginPair globalPlugin in PluginManager.GlobalPlugins)
                {
                    if (!pluginQueryPair.ContainsKey(globalPlugin))
                    {
                        var query = new Query(rawQuery, rawQuery, new ReadOnlyCollection<string>(terms), string.Empty);
                        pluginQueryPair.Add(globalPlugin, query);
                    }
                }
            }

            return pluginQueryPair;
        }
    }
}
