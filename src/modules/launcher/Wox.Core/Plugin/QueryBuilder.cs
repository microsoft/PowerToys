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
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            if (nonGlobalPlugins == null)
            {
                throw new ArgumentNullException(nameof(nonGlobalPlugins));
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

            foreach (string pluginActionKeyword in nonGlobalPlugins.Keys)
            {
                // Using InvariantCulture since this is a command line arg
                if (possibleActionKeyword.StartsWith(pluginActionKeyword, StringComparison.InvariantCulture))
                {
                    if (nonGlobalPlugins.TryGetValue(pluginActionKeyword, out var pluginPair) && !pluginPair.Metadata.Disabled)
                    {
                        // The search string is the raw query excluding the action keyword
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
                        var query = new Query(rawQuery, search, terms, pluginActionKeyword);

                        pluginQueryPair.TryAdd(pluginPair, query);
                    }
                }
            }

            var globalplugins = PluginManager.GlobalPlugins;

            foreach (PluginPair globalPlugin in PluginManager.GlobalPlugins)
            {
                var query = new Query(rawQuery, rawQuery, terms, string.Empty);
                pluginQueryPair.Add(globalPlugin, query);
            }

            return pluginQueryPair;
        }
    }
}
