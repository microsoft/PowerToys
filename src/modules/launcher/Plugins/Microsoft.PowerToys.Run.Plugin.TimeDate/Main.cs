// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Controls;
using System.Windows.Input;
using ManagedCommon;
using Microsoft.PowerToys.Run.Plugin.TimeDate.Components;
using Microsoft.PowerToys.Run.Plugin.TimeDate.Properties;
using Microsoft.PowerToys.Settings.UI.Library;
using Wox.Plugin;

namespace Microsoft.PowerToys.Run.Plugin.TimeDate
{
    public class Main : IPlugin, IPluginI18n, ISettingProvider, IContextMenu
    {
        private PluginInitContext _context;

        public string IconTheme { get; set; }

        public string Name => Resources.Microsoft_plugin_timedate_plugin_name;

        public string Description => GetTranslatedPluginDescription();

        public static string PluginID => "5D69806A5A474115821C3E4C56B9C793";

        private static readonly CompositeFormat MicrosoftPluginTimedatePluginDescription = System.Text.CompositeFormat.Parse(Properties.Resources.Microsoft_plugin_timedate_plugin_description);

        public IEnumerable<PluginAdditionalOption> AdditionalOptions
        {
            get
            {
                return TimeDateSettings.GetAdditionalOptions();
            }
        }

        public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
        {
            if (!(selectedResult?.ContextData is AvailableResult data))
            {
                return new List<ContextMenuResult>(0);
            }

            return new List<ContextMenuResult>()
            {
                new ContextMenuResult
                {
                    AcceleratorKey = Key.C,
                    AcceleratorModifiers = ModifierKeys.Control,
                    FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                    Glyph = "\xE8C8",                       // E8C8 => Symbol: Copy
                    Title = Resources.Microsoft_plugin_timedate_CopyToClipboard,
                    Action = _ => ResultHelper.CopyToClipBoard(data.Value),
                },
            };
        }

        public void Init(PluginInitContext context)
        {
            _context = context;
            _context.API.ThemeChanged += OnThemeChanged;
            UpdateIconTheme(_context.API.GetCurrentTheme());
        }

        public List<Result> Query(Query query)
        {
            ArgumentNullException.ThrowIfNull(query);

            return SearchController.ExecuteSearch(query, IconTheme);
        }

        private void UpdateIconTheme(Theme theme)
        {
            if (theme == Theme.Light || theme == Theme.HighContrastWhite)
            {
                IconTheme = "light";
            }
            else
            {
                IconTheme = "dark";
            }
        }

        private void OnThemeChanged(Theme currentTheme, Theme newTheme)
        {
            UpdateIconTheme(newTheme);
        }

        public string GetTranslatedPluginDescription()
        {
            // The extra strings for the examples are required for correct translations.
            string timeExample = Resources.Microsoft_plugin_timedate_plugin_description_example_time + "::" + DateTime.Now.ToString("T", CultureInfo.CurrentCulture);
            string dayExample = Resources.Microsoft_plugin_timedate_plugin_description_example_day + "::" + DateTime.Now.ToString("d", CultureInfo.CurrentCulture);
            string calendarWeekExample = Resources.Microsoft_plugin_timedate_plugin_description_example_calendarWeek + "::" + DateTime.Now.ToString("d", CultureInfo.CurrentCulture);
            return string.Format(CultureInfo.CurrentCulture, MicrosoftPluginTimedatePluginDescription, Resources.Microsoft_plugin_timedate_plugin_description_example_day, dayExample, timeExample, calendarWeekExample);
        }

        public string GetTranslatedPluginTitle()
        {
            return Resources.Microsoft_plugin_timedate_plugin_name;
        }

        public Control CreateSettingPanel()
        {
            throw new NotImplementedException();
        }

        public void UpdateSettings(PowerLauncherPluginSettings settings)
        {
            TimeDateSettings.Instance.UpdateSettings(settings);
        }
    }
}
