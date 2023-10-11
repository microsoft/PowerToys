// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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

        /* The two property lines are commented because they break the unit tests. (The Moq package used in the unit tests doesn't support the .Net 7 feature 'static abstract' properties yet.) - https://github.com/Moq/Moq/issues/1398
        *
        * // Plugin ID for validating the plugin.json entry (It must be static for accessing it before loading the plugin.)
        * public static abstract string PluginID { get; }
        */
    }
}
