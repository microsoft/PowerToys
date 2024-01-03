// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Wox.Plugin;

namespace PowerLauncher.Plugin
{
    public static class QueryBuilder
    {
        public static Dictionary<PluginPair, Query> Build(string text)
        {
            ArgumentNullException.ThrowIfNull(text);

            text = text.Trim();
            int longestActionKeywordLength = 0;

            // This Dictionary contains the corresponding query for each plugin
            Dictionary<PluginPair, Query> pluginQueryPair = new Dictionary<PluginPair, Query>();
            foreach (var nonGlobalPlugin in PluginManager.NonGlobalPlugins)
            {
                var pluginActionKeyword = nonGlobalPlugin.Metadata.ActionKeyword;
                if (nonGlobalPlugin.Metadata.Disabled || !text.StartsWith(pluginActionKeyword, StringComparison.Ordinal))
                {
                    continue;
                }

                // Save the length of the longest matching keyword for later use
                if (pluginActionKeyword.Length > longestActionKeywordLength)
                {
                    longestActionKeywordLength = pluginActionKeyword.Length;
                }

                // A new query is constructed for each plugin
                var query = new Query(text, pluginActionKeyword);
                pluginQueryPair.TryAdd(nonGlobalPlugin, query);
            }

            // If we have plugin action keywords that start with the same char we get false positives (Example: ? and ??)
            // Here we remove each query pair that has a shorter keyword than the longest matching one
            foreach (PluginPair plugin in pluginQueryPair.Keys)
            {
                if (plugin.Metadata.ActionKeyword.Length < longestActionKeywordLength)
                {
                    pluginQueryPair.Remove(plugin);
                }
            }

            // If the user has specified a matching action keyword, then do not
            // add the global plugins to the list.
            if (pluginQueryPair.Count == 0)
            {
                foreach (PluginPair globalPlugin in PluginManager.GlobalPlugins)
                {
                    if (!globalPlugin.Metadata.Disabled && !pluginQueryPair.ContainsKey(globalPlugin))
                    {
                        var query = new Query(text);
                        pluginQueryPair.Add(globalPlugin, query);
                    }
                }
            }

            return pluginQueryPair;
        }
    }
}
