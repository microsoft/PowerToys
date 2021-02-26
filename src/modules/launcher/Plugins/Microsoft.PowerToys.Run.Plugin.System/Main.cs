// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Interop;
using ManagedCommon;
using Microsoft.PowerToys.Run.Plugin.System.Win32;
using Wox.Infrastructure;
using Wox.Plugin;

namespace Microsoft.PowerToys.Run.Plugin.System
{
    public class Main : IPlugin, IPluginI18n
    {
        private PluginInitContext _context;

        internal const int EWXLOGOFF = 0x00000000;
        internal const int EWXSHUTDOWN = 0x00000001;
        internal const int EWXREBOOT = 0x00000002;
        internal const int EWXFORCE = 0x00000004;
        internal const int EWXPOWEROFF = 0x00000008;
        internal const int EWXFORCEIFHUNG = 0x00000010;

        public string IconTheme { get; set; }

        public string Name => Properties.Resources.Microsoft_plugin_sys_plugin_name;

        public string Description => Properties.Resources.Microsoft_plugin_sys_plugin_description;

        public void Init(PluginInitContext context)
        {
            this._context = context;
            _context.API.ThemeChanged += OnThemeChanged;
            UpdateIconTheme(_context.API.GetCurrentTheme());
        }

        public List<Result> Query(Query query)
        {
            if (query == null)
            {
                throw new ArgumentNullException(paramName: nameof(query));
            }

            var commands = Commands();
            var results = new List<Result>();

            foreach (var c in commands)
            {
                var titleMatch = StringMatcher.FuzzySearch(query.Search, c.Title);
                if (titleMatch.Score > 0)
                {
                    c.Score = titleMatch.Score;
                    c.SetTitleHighlightData(titleMatch.MatchData);
                    results.Add(c);
                }
            }

            return results;
        }

        private List<Result> Commands()
        {
            var results = new List<Result>();
            results.AddRange(new[]
            {
                new Result
                {
                    Title = Properties.Resources.Microsoft_plugin_sys_shutdown_computer,
                    SubTitle = Properties.Resources.Microsoft_plugin_sys_shutdown_computer_description,
                    IcoPath = $"Images\\shutdown.{IconTheme}.png",
                    Action = c =>
                    {
                        Helper.OpenInShell("shutdown", "/s /t 0");
                        return true;
                    },
                },
                new Result
                {
                    Title = Properties.Resources.Microsoft_plugin_sys_restart_computer,
                    SubTitle = Properties.Resources.Microsoft_plugin_sys_restart_computer_description,
                    IcoPath = $"Images\\restart.{IconTheme}.png",
                    Action = c =>
                    {
                        Helper.OpenInShell("shutdown", "/r /t 0");
                        return true;
                    },
                },
                new Result
                {
                    Title = Properties.Resources.Microsoft_plugin_sys_sign_out,
                    SubTitle = Properties.Resources.Microsoft_plugin_sys_sign_out_description,
                    IcoPath = $"Images\\logoff.{IconTheme}.png",
                    Action = c =>
                    {
                        NativeMethods.ExitWindowsEx(EWXLOGOFF, 0);
                        return true;
                    },
                },
                new Result
                {
                    Title = Properties.Resources.Microsoft_plugin_sys_lock,
                    SubTitle = Properties.Resources.Microsoft_plugin_sys_lock_description,
                    IcoPath = $"Images\\lock.{IconTheme}.png",
                    Action = c =>
                    {
                        NativeMethods.LockWorkStation();
                        return true;
                    },
                },
                new Result
                {
                    Title = Properties.Resources.Microsoft_plugin_sys_sleep,
                    SubTitle = Properties.Resources.Microsoft_plugin_sys_sleep_description,
                    IcoPath = $"Images\\sleep.{IconTheme}.png",
                    Action = c =>
                    {
                        NativeMethods.SetSuspendState(false, true, true);
                        return true;
                    },
                },
                new Result
                {
                    Title = Properties.Resources.Microsoft_plugin_sys_hibernate,
                    SubTitle = Properties.Resources.Microsoft_plugin_sys_hibernate_description,
                    IcoPath = $"Images\\sleep.{IconTheme}.png", // Icon change needed
                    Action = c =>
                    {
                        NativeMethods.SetSuspendState(true, true, true);
                        return true;
                    },
                },
                new Result
                {
                    Title = Properties.Resources.Microsoft_plugin_sys_emptyrecyclebin,
                    SubTitle = Properties.Resources.Microsoft_plugin_sys_emptyrecyclebin_description,
                    IcoPath = $"Images\\recyclebin.{IconTheme}.png",
                    Action = c =>
                    {
                        // http://www.pinvoke.net/default.aspx/shell32/SHEmptyRecycleBin.html
                        // FYI, couldn't find documentation for this but if the recycle bin is already empty, it will return -2147418113 (0x8000FFFF (E_UNEXPECTED))
                        // 0 for nothing
                        var result = NativeMethods.SHEmptyRecycleBin(new WindowInteropHelper(Application.Current.MainWindow).Handle, 0);
                        if (result != (uint)NativeMethods.HRESULT.S_OK && result != 0x8000FFFF)
                        {
                            var name = "Plugin: " + Properties.Resources.Microsoft_plugin_sys_plugin_name;
                            var message = $"Error emptying recycle bin, error code: {result}\n" +
                                          "please refer to https://msdn.microsoft.com/en-us/library/windows/desktop/aa378137";
                            _context.API.ShowMsg(name, message);
                        }

                        return true;
                    },
                },
            });
            return results;
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
            return Properties.Resources.Microsoft_plugin_sys_plugin_description;
        }

        public string GetTranslatedPluginTitle()
        {
            return Properties.Resources.Microsoft_plugin_sys_plugin_name;
        }
    }
}
