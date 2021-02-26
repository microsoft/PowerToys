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
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            text = text.Trim();

            // This Dictionary contains the corresponding query for each plugin
            Dictionary<PluginPair, Query> pluginQueryPair = new Dictionary<PluginPair, Query>();
            foreach (var plugin in PluginManager.NonGlobalPlugins)
            {
                var pluginActionKeyword = plugin.Metadata.ActionKeyword;
                if (plugin.Metadata.Disabled || !text.StartsWith(pluginActionKeyword, StringComparison.Ordinal))
                {
                    continue;
                }

                // A new query is constructed for each plugin
                var query = new Query(text, pluginActionKeyword);
                pluginQueryPair.TryAdd(plugin, query);
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
