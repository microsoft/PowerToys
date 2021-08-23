// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using ManagedCommon;
using Microsoft.PowerToys.Run.Plugin.System.Win32;
using Microsoft.PowerToys.Settings.UI.Library;
using Wox.Infrastructure;
using Wox.Infrastructure.Storage;
using Wox.Plugin;

namespace Microsoft.PowerToys.Run.Plugin.System
{
    public class Main : IPlugin, IPluginI18n, ISettingProvider
    {
        private PluginInitContext _context;
        private const string ConfirmSystemCommands = nameof(ConfirmSystemCommands);

        internal const int EWXLOGOFF = 0x00000000;
        internal const int EWXSHUTDOWN = 0x00000001;
        internal const int EWXREBOOT = 0x00000002;
        internal const int EWXFORCE = 0x00000004;
        internal const int EWXPOWEROFF = 0x00000008;
        internal const int EWXFORCEIFHUNG = 0x00000010;

        public string IconTheme { get; set; }

        public ICommand Command { get; set; }

        public string Name => Properties.Resources.Microsoft_plugin_sys_plugin_name;

        public string Description => Properties.Resources.Microsoft_plugin_sys_plugin_description;

        private bool _confirmSystemCommands;

        public IEnumerable<PluginAdditionalOption> AdditionalOptions => new List<PluginAdditionalOption>()
        {
            new PluginAdditionalOption()
            {
                Key = ConfirmSystemCommands,
                DisplayLabel = Properties.Resources.confirm_system_commands,
                Value = false,
            },
        };

        public void Init(PluginInitContext context)
        {
            _context = context;
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
                    c.TitleHighlightData = titleMatch.MatchData;
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
                        return ExecuteCommand(Properties.Resources.Microsoft_plugin_sys_shutdown_computer_confirmation, () => Helper.OpenInShell("shutdown", "/s /t 0"));
                    },
                },
                new Result
                {
                    Title = Properties.Resources.Microsoft_plugin_sys_restart_computer,
                    SubTitle = Properties.Resources.Microsoft_plugin_sys_restart_computer_description,
                    IcoPath = $"Images\\restart.{IconTheme}.png",
                    Action = c =>
                    {
                        return ExecuteCommand(Properties.Resources.Microsoft_plugin_sys_restart_computer_confirmation, () => Helper.OpenInShell("shutdown", "/r /t 0"));
                    },
                },
                new Result
                {
                    Title = Properties.Resources.Microsoft_plugin_sys_sign_out,
                    SubTitle = Properties.Resources.Microsoft_plugin_sys_sign_out_description,
                    IcoPath = $"Images\\logoff.{IconTheme}.png",
                    Action = c =>
                    {
                        return ExecuteCommand(Properties.Resources.Microsoft_plugin_sys_sign_out_confirmation, () => NativeMethods.ExitWindowsEx(EWXLOGOFF, 0));
                    },
                },
                new Result
                {
                    Title = Properties.Resources.Microsoft_plugin_sys_lock,
                    SubTitle = Properties.Resources.Microsoft_plugin_sys_lock_description,
                    IcoPath = $"Images\\lock.{IconTheme}.png",
                    Action = c =>
                    {
                        return ExecuteCommand(Properties.Resources.Microsoft_plugin_sys_lock_confirmation, () => NativeMethods.LockWorkStation());
                    },
                },
                new Result
                {
                    Title = Properties.Resources.Microsoft_plugin_sys_sleep,
                    SubTitle = Properties.Resources.Microsoft_plugin_sys_sleep_description,
                    IcoPath = $"Images\\sleep.{IconTheme}.png",
                    Action = c =>
                    {
                        return ExecuteCommand(Properties.Resources.Microsoft_plugin_sys_sleep_confirmation, () => NativeMethods.SetSuspendState(false, true, true));
                    },
                },
                new Result
                {
                    Title = Properties.Resources.Microsoft_plugin_sys_hibernate,
                    SubTitle = Properties.Resources.Microsoft_plugin_sys_hibernate_description,
                    IcoPath = $"Images\\sleep.{IconTheme}.png", // Icon change needed
                    Action = c =>
                    {
                        return ExecuteCommand(Properties.Resources.Microsoft_plugin_sys_hibernate_confirmation, () => NativeMethods.SetSuspendState(true, true, true));
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

        private bool ExecuteCommand(string confirmationMessage, Action command)
        {
            if (_confirmSystemCommands)
            {
                MessageBoxResult messageBoxResult = MessageBox.Show(
                    confirmationMessage,
                    Properties.Resources.Microsoft_plugin_sys_confirmation,
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (messageBoxResult == MessageBoxResult.No)
                {
                    return false;
                }
            }

            command();
            return true;
        }

        public Control CreateSettingPanel()
        {
            throw new NotImplementedException();
        }

        public void UpdateSettings(PowerLauncherPluginSettings settings)
        {
            var confirmSystemCommands = false;

            if (settings != null && settings.AdditionalOptions != null)
            {
                var option = settings.AdditionalOptions.FirstOrDefault(x => x.Key == ConfirmSystemCommands);

                confirmSystemCommands = option == null ? false : option.Value;
            }

            _confirmSystemCommands = confirmSystemCommands;
        }
    }
}
