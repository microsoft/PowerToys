// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Windows.Controls;
using ManagedCommon;
using Microsoft.Plugin.WindowWalker.Components;
using Microsoft.PowerToys.Settings.UI.Library;
using Wox.Plugin;
using Wox.Plugin.Common.VirtualDesktop.Helper;

namespace Microsoft.Plugin.WindowWalker
{
    public class Main : IPlugin, IPluginI18n, ISettingProvider, IContextMenu
    {
        private string IconPath { get; set; }

        private string InfoIconPath { get; set; }

        private PluginInitContext Context { get; set; }

        public string Name => Properties.Resources.wox_plugin_windowwalker_plugin_name;

        public string Description => Properties.Resources.wox_plugin_windowwalker_plugin_description;

        internal static readonly VirtualDesktopHelper VirtualDesktopHelperInstance = new VirtualDesktopHelper();

        static Main()
        {
            OpenWindows.Instance.UpdateOpenWindowsList();
        }

        public List<Result> Query(Query query)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            VirtualDesktopHelperInstance.UpdateDesktopList();
            OpenWindows.Instance.UpdateOpenWindowsList();
            SearchController.Instance.UpdateSearchText(query.Search);
            List<SearchResult> searchControllerResults = SearchController.Instance.SearchMatches;

            return ResultHelper.GetResultList(searchControllerResults, !string.IsNullOrEmpty(query.ActionKeyword), IconPath, InfoIconPath);
        }

        public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
        {
            return ContextMenuHelper.GetContextMenuResults(selectedResult);
        }

        public void Init(PluginInitContext context)
        {
            Context = context;
            Context.API.ThemeChanged += OnThemeChanged;
            UpdateIconPath(Context.API.GetCurrentTheme());
        }

        public IEnumerable<PluginAdditionalOption> AdditionalOptions
        {
            get { return WindowWalkerSettings.GetAdditionalOptions(); }
        }

        // Todo : Update with theme based IconPath
        private void UpdateIconPath(Theme theme)
        {
            if (theme == Theme.Light || theme == Theme.HighContrastWhite)
            {
                IconPath = "Images/windowwalker.light.png";
                InfoIconPath = "Images/info.light.png";
            }
            else
            {
                IconPath = "Images/windowwalker.dark.png";
                InfoIconPath = "Images/info.dark.png";
            }
        }

        private void OnThemeChanged(Theme currentTheme, Theme newTheme)
        {
            UpdateIconPath(newTheme);
        }

        public string GetTranslatedPluginTitle()
        {
            return Properties.Resources.wox_plugin_windowwalker_plugin_name;
        }

        public string GetTranslatedPluginDescription()
        {
            return Properties.Resources.wox_plugin_windowwalker_plugin_description;
        }

        public Control CreateSettingPanel()
        {
            throw new NotImplementedException();
        }

        public void UpdateSettings(PowerLauncherPluginSettings settings)
        {
            WindowWalkerSettings.Instance.UpdateSettings(settings);
        }
    }
}
