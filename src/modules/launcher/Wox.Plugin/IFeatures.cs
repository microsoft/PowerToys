// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Wox.Plugin
{
    public interface IFeatures
    {
    }

    public interface IContextMenu : IFeatures
    {
        List<ContextMenuResult> LoadContextMenus(Result selectedResult);
    }

    /// <summary>
    /// Represent plugins that support internationalization
    /// </summary>
    public interface IPluginI18n : IFeatures
    {
        string GetTranslatedPluginTitle();

        string GetTranslatedPluginDescription();
    }

    public interface IResultUpdated : IFeatures
    {
        event ResultUpdatedEventHandler ResultsUpdated;
    }

    public delegate void ResultUpdatedEventHandler(IResultUpdated sender, ResultUpdatedEventArgs e);
}
