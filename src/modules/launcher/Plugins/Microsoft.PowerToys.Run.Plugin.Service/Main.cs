// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Input;
using ManagedCommon;
using Microsoft.PowerToys.Run.Plugin.Service.Helpers;
using Microsoft.PowerToys.Run.Plugin.Service.Properties;
using Wox.Plugin;

namespace Microsoft.PowerToys.Run.Plugin.Service
{
    public class Main : IPlugin, IContextMenu, IPluginI18n
    {
        private PluginInitContext _context;
        private string _icoPath;

        public void Init(PluginInitContext context)
        {
            _context = context;
            _context.API.ThemeChanged += OnThemeChanged;

            UpdateIconPath(_context.API.GetCurrentTheme());
        }

        public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
        {
            if (!(selectedResult?.ContextData is ServiceResult))
            {
                return new List<ContextMenuResult>();
            }

            var contextMenuResult = new List<ContextMenuResult>();
            var serviceResult = selectedResult.ContextData as ServiceResult;

            if (serviceResult.IsRunning)
            {
                // Stop
                contextMenuResult.Add(new ContextMenuResult
                {
                    PluginName = Assembly.GetExecutingAssembly().GetName().Name,
                    Title = Resources.wox_plugin_service_stop,
                    Glyph = "\xE71A",
                    FontFamily = "Segoe MDL2 Assets",
                    AcceleratorKey = Key.Enter,
                    Action = _ =>
                    {
                        Task.Run(() => ServiceHelper.ChangeStatus(serviceResult, Action.Stop, _context.API));
                        return true;
                    },
                });

                // Restart
                contextMenuResult.Add(new ContextMenuResult
                {
                    PluginName = Assembly.GetExecutingAssembly().GetName().Name,
                    Title = Resources.wox_plugin_service_restart,
                    Glyph = "\xE72C",
                    FontFamily = "Segoe MDL2 Assets",
                    AcceleratorKey = Key.R,
                    AcceleratorModifiers = ModifierKeys.Control | ModifierKeys.Shift,
                    Action = _ =>
                    {
                        Task.Run(() => ServiceHelper.ChangeStatus(serviceResult, Action.Restart, _context.API));
                        return true;
                    },
                });
            }
            else
            {
                // Start
                contextMenuResult.Add(new ContextMenuResult
                {
                    PluginName = Assembly.GetExecutingAssembly().GetName().Name,
                    Title = Resources.wox_plugin_service_start,
                    Glyph = "\xEDB5",
                    FontFamily = "Segoe MDL2 Assets",
                    AcceleratorKey = Key.Enter,
                    Action = _ =>
                    {
                        Task.Run(() => ServiceHelper.ChangeStatus(serviceResult, Action.Start, _context.API));
                        return true;
                    },
                });
            }

            // Open services
            contextMenuResult.Add(new ContextMenuResult
            {
                PluginName = Assembly.GetExecutingAssembly().GetName().Name,
                Title = Resources.wox_plugin_service_open_services,
                Glyph = "\xE8A7",
                FontFamily = "Segoe MDL2 Assets",
                AcceleratorKey = Key.O,
                AcceleratorModifiers = ModifierKeys.Control | ModifierKeys.Shift,
                Action = _ =>
                {
                    Task.Run(() => ServiceHelper.OpenServices());
                    return true;
                },
            });

            return contextMenuResult;
        }

        public List<Result> Query(Query query)
        {
            var search = query?.Search ?? string.Empty;
            return ServiceHelper.Search(search, _icoPath).ToList();
        }

        public string GetTranslatedPluginTitle()
        {
            return Resources.wox_plugin_service_plugin_name;
        }

        public string GetTranslatedPluginDescription()
        {
            return Resources.wox_plugin_service_plugin_description;
        }

        private void UpdateIconPath(Theme theme)
        {
            if (theme == Theme.Light || theme == Theme.HighContrastWhite)
            {
                _icoPath = "Images/service.light.png";
            }
            else
            {
                _icoPath = "Images/service.dark.png";
            }
        }

        private void OnThemeChanged(Theme currentTheme, Theme newTheme)
        {
            UpdateIconPath(newTheme);
        }
    }
}
