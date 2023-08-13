// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Wox.Plugin
{
    public interface IPlugin
    {
        List<Result> Query(Query query);

        void Init(PluginInitContext context);

        // Localized name
        string Name { get; }

        // Localized description
        string Description { get; }

        // Plugin ID for validating the plugin.json entry (It must be static for accessing it before loading the plugin.)
        public static abstract string PluginID { get; }
    }
}
