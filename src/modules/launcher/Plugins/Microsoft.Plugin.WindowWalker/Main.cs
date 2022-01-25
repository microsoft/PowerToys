// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using ManagedCommon;
using Microsoft.Plugin.WindowWalker.Components;
using Wox.Plugin;

namespace Microsoft.Plugin.WindowWalker
{
    public class Main : IPlugin, IPluginI18n
    {
        private string IconPath { get; set; }

        private PluginInitContext Context { get; set; }

        public string Name => Properties.Resources.wox_plugin_windowwalker_plugin_name;

        public string Description => Properties.Resources.wox_plugin_windowwalker_plugin_description;

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

            OpenWindows.Instance.UpdateOpenWindowsList();
            SearchController.Instance.UpdateSearchText(query.Search);
            List<SearchResult> searchControllerResults = SearchController.Instance.SearchMatches;

            return searchControllerResults.Select(x => new Result()
            {
                Title = x.Result.Title,
                IcoPath = IconPath,
                SubTitle = Properties.Resources.wox_plugin_windowwalker_running + ": " + x.Result.ProcessInfo.Name,
                Action = c =>
                {
                    x.Result.SwitchToWindow();
                    return true;
                },

                // For debugging you can remove the comment sign in the next line.
                // ToolTipData = new ToolTipData(x.Result.Title, $"hWnd: {x.Result.Hwnd}\nWindow class: {x.Result.ClassName}\nProcess ID: {x.Result.ProcessInfo.ProcessID}\nThread ID: {x.Result.ProcessInfo.ThreadID}\nProcess: {x.Result.ProcessInfo.Name}\nProcess exists: {x.Result.ProcessInfo.DoesExist}\nIs full access denied: {x.Result.ProcessInfo.IsFullAccessDenied}\nIs uwp app: {x.Result.ProcessInfo.IsUwpApp}\nIs ShellProcess: {x.Result.ProcessInfo.IsShellProcess}\nIs window cloaked: {x.Result.IsCloaked}\nWindow cloak state: {x.Result.GetWindowCloakState()}"),
            }).ToList();
        }

        public void Init(PluginInitContext context)
        {
            Context = context;
            Context.API.ThemeChanged += OnThemeChanged;
            UpdateIconPath(Context.API.GetCurrentTheme());
        }

        // Todo : Update with theme based IconPath
        private void UpdateIconPath(Theme theme)
        {
            if (theme == Theme.Light || theme == Theme.HighContrastWhite)
            {
                IconPath = "Images/windowwalker.light.png";
            }
            else
            {
                IconPath = "Images/windowwalker.dark.png";
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
    }
}
