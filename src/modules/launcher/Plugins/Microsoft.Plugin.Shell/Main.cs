// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Input;
using ManagedCommon;
using Microsoft.Plugin.Shell.Properties;
using Microsoft.PowerToys.Settings.UI.Library;
using Wox.Infrastructure.Storage;
using Wox.Plugin;
using Wox.Plugin.Common;
using Wox.Plugin.Logger;
using Control = System.Windows.Controls.Control;

namespace Microsoft.Plugin.Shell
{
    public class Main : IPlugin, IPluginI18n, ISettingProvider, IContextMenu, ISavable
    {
        private static readonly IFileSystem FileSystem = new FileSystem();
        private static readonly IPath Path = FileSystem.Path;
        private static readonly IFile File = FileSystem.File;
        private static readonly IDirectory Directory = FileSystem.Directory;

        private readonly ShellPluginSettings _settings;
        private readonly PluginJsonStorage<ShellPluginSettings> _storage;

        private static readonly CompositeFormat WoxPluginCmdCmdHasBeenExecutedTimes = System.Text.CompositeFormat.Parse(Properties.Resources.wox_plugin_cmd_cmd_has_been_executed_times);

        private string IconPath { get; set; }

        public string Name => Properties.Resources.wox_plugin_cmd_plugin_name;

        public string Description => Properties.Resources.wox_plugin_cmd_plugin_description;

        public static string PluginID => "D409510CD0D2481F853690A07E6DC426";

        public IEnumerable<PluginAdditionalOption> AdditionalOptions => new List<PluginAdditionalOption>()
        {
            new PluginAdditionalOption()
            {
                Key = "ShellCommandExecution",
                DisplayLabel = Resources.wox_shell_command_execution,
                DisplayDescription = Resources.wox_shell_command_execution_description,
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Combobox,
                ComboBoxItems = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>(Resources.find_executable_file_and_run_it, "2"),
                    new KeyValuePair<string, string>(Resources.run_command_in_command_prompt, "0"),
                    new KeyValuePair<string, string>(Resources.run_command_in_powershell, "1"),
                    new KeyValuePair<string, string>(Resources.run_command_in_powershell_seven, "6"),
                    new KeyValuePair<string, string>(Resources.run_command_in_windows_terminal_cmd, "5"),
                    new KeyValuePair<string, string>(Resources.run_command_in_windows_terminal_powershell, "3"),
                    new KeyValuePair<string, string>(Resources.run_command_in_windows_terminal_powershell_seven, "4"),
                },
                ComboBoxValue = (int)_settings.Shell,
            },

            new PluginAdditionalOption()
            {
                Key = "LeaveShellOpen",
                DisplayLabel = Resources.wox_leave_shell_open,
                Value = _settings.LeaveShellOpen,
            },
        };

        private PluginInitContext _context;
        private static readonly char[] Separator = new[] { ' ' };

        public Main()
        {
            _storage = new PluginJsonStorage<ShellPluginSettings>();
            _settings = _storage.Load();
        }

        public void Save()
        {
            _storage.Save();
        }

        public List<Result> Query(Query query)
        {
            ArgumentNullException.ThrowIfNull(query);

            List<Result> results = new List<Result>();
            string cmd = query.Search;
            if (string.IsNullOrEmpty(cmd))
            {
                return ResultsFromlHistory();
            }
            else
            {
                var queryCmd = GetCurrentCmd(cmd);
                results.Add(queryCmd);
                var history = GetHistoryCmds(cmd, queryCmd);
                results.AddRange(history);

                try
                {
                    IEnumerable<Result> folderPluginResults = Folder.Main.GetFolderPluginResults(query);
                    results.AddRange(folderPluginResults);
                }
                catch (Exception e)
                {
                    Log.Exception($"Exception when query for <{query}>", e, GetType());
                }

                return results;
            }
        }

        private List<Result> GetHistoryCmds(string cmd, Result result)
        {
            IEnumerable<Result> history = _settings.Count.Where(o => o.Key.Contains(cmd, StringComparison.CurrentCultureIgnoreCase))
                .OrderByDescending(o => o.Value)
                .Select(m =>
                {
                    if (m.Key == cmd)
                    {
                        // Using CurrentCulture since this is user facing
                        result.SubTitle = Properties.Resources.wox_plugin_cmd_plugin_name + ": " + string.Format(CultureInfo.CurrentCulture, WoxPluginCmdCmdHasBeenExecutedTimes, m.Value);
                        return null;
                    }

                    var ret = new Result
                    {
                        Title = m.Key,

                        // Using CurrentCulture since this is user facing
                        SubTitle = Properties.Resources.wox_plugin_cmd_plugin_name + ": " + string.Format(CultureInfo.CurrentCulture, WoxPluginCmdCmdHasBeenExecutedTimes, m.Value),
                        IcoPath = IconPath,
                        Action = c =>
                        {
                            Execute(Process.Start, PrepareProcessStartInfo(m.Key));
                            return true;
                        },
                    };
                    return ret;
                }).Where(o => o != null).Take(4);
            return history.ToList();
        }

        private Result GetCurrentCmd(string cmd)
        {
            Result result = new Result
            {
                Title = cmd,
                Score = 5000,
                SubTitle = Properties.Resources.wox_plugin_cmd_plugin_name + ": " + Properties.Resources.wox_plugin_cmd_execute_through_shell,
                IcoPath = IconPath,
                Action = c =>
                {
                    Execute(Process.Start, PrepareProcessStartInfo(cmd));
                    return true;
                },
            };

            return result;
        }

        private List<Result> ResultsFromlHistory()
        {
            IEnumerable<Result> history = _settings.Count.OrderByDescending(o => o.Value)
                .Select(m => new Result
                {
                    Title = m.Key,

                    // Using CurrentCulture since this is user facing
                    SubTitle = Properties.Resources.wox_plugin_cmd_plugin_name + ": " + string.Format(CultureInfo.CurrentCulture, WoxPluginCmdCmdHasBeenExecutedTimes, m.Value),
                    IcoPath = IconPath,
                    Action = c =>
                    {
                        Execute(Process.Start, PrepareProcessStartInfo(m.Key));
                        return true;
                    },
                }).Take(5);
            return history.ToList();
        }

        private ProcessStartInfo PrepareProcessStartInfo(string command, RunAsType runAs = RunAsType.None)
        {
            string trimmedCommand = command.Trim();
            command = Environment.ExpandEnvironmentVariables(trimmedCommand);
            var workingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            // Set runAsArg
            string runAsVerbArg = string.Empty;
            if (runAs == RunAsType.OtherUser)
            {
                runAsVerbArg = "runAsUser";
            }
            else if (runAs == RunAsType.Administrator || _settings.RunAsAdministrator)
            {
                runAsVerbArg = "runAs";
            }

            ProcessStartInfo info;
            if (_settings.Shell == ExecutionShell.Cmd)
            {
                var arguments = _settings.LeaveShellOpen ? $"/k \"{command}\"" : $"/c \"{command}\" & pause";

                info = ShellCommand.SetProcessStartInfo("cmd.exe", workingDirectory, arguments, runAsVerbArg);
            }
            else if (_settings.Shell == ExecutionShell.Powershell)
            {
                string arguments;
                if (_settings.LeaveShellOpen)
                {
                    arguments = $"-NoExit \"{command}\"";
                }
                else
                {
                    arguments = $"\"{command} ; Read-Host -Prompt \\\"{Resources.run_plugin_cmd_wait_message}\\\"\"";
                }

                info = ShellCommand.SetProcessStartInfo("powershell.exe", workingDirectory, arguments, runAsVerbArg);
            }
            else if (_settings.Shell == ExecutionShell.PowerShellSeven)
            {
                string arguments;
                if (_settings.LeaveShellOpen)
                {
                    arguments = $"-NoExit -C \"{command}\"";
                }
                else
                {
                    arguments = $"-C \"{command} ; Read-Host -Prompt \\\"{Resources.run_plugin_cmd_wait_message}\\\"\"";
                }

                info = ShellCommand.SetProcessStartInfo("pwsh.exe", workingDirectory, arguments, runAsVerbArg);
            }
            else if (_settings.Shell == ExecutionShell.WindowsTerminalCmd)
            {
                string arguments;
                if (_settings.LeaveShellOpen)
                {
                    arguments = $"cmd.exe /k \"{command}\"";
                }
                else
                {
                    arguments = $"cmd.exe /c \"{command}\" & pause";
                }

                info = ShellCommand.SetProcessStartInfo("wt.exe", workingDirectory, arguments, runAsVerbArg);
            }
            else if (_settings.Shell == ExecutionShell.WindowsTerminalPowerShell)
            {
                string arguments;
                if (_settings.LeaveShellOpen)
                {
                    arguments = $"powershell -NoExit -C \"{command}\"";
                }
                else
                {
                    arguments = $"powershell -C \"{command}\"";
                }

                info = ShellCommand.SetProcessStartInfo("wt.exe", workingDirectory, arguments, runAsVerbArg);
            }
            else if (_settings.Shell == ExecutionShell.WindowsTerminalPowerShellSeven)
            {
                string arguments;
                if (_settings.LeaveShellOpen)
                {
                    arguments = $"pwsh.exe -NoExit -C \"{command}\"";
                }
                else
                {
                    arguments = $"pwsh.exe -C \"{command}\"";
                }

                info = ShellCommand.SetProcessStartInfo("wt.exe", workingDirectory, arguments, runAsVerbArg);
            }
            else if (_settings.Shell == ExecutionShell.RunCommand)
            {
                // Open explorer if the path is a file or directory
                if (Directory.Exists(command) || File.Exists(command))
                {
                    info = ShellCommand.SetProcessStartInfo("explorer.exe", arguments: command, verb: runAsVerbArg);
                }
                else
                {
                    var parts = command.Split(Separator, 2);
                    if (parts.Length == 2)
                    {
                        var filename = parts[0];
                        if (ExistInPath(filename))
                        {
                            var arguments = parts[1];
                            if (_settings.LeaveShellOpen)
                            {
                                // Wrap the command in a cmd.exe process
                                info = ShellCommand.SetProcessStartInfo("cmd.exe", workingDirectory, $"/k \"{filename} {arguments}\"", runAsVerbArg);
                            }
                            else
                            {
                                info = ShellCommand.SetProcessStartInfo(filename, workingDirectory, arguments, runAsVerbArg);
                            }
                        }
                        else
                        {
                            if (_settings.LeaveShellOpen)
                            {
                                // Wrap the command in a cmd.exe process
                                info = ShellCommand.SetProcessStartInfo("cmd.exe", workingDirectory, $"/k \"{command}\"", runAsVerbArg);
                            }
                            else
                            {
                                info = ShellCommand.SetProcessStartInfo(command, verb: runAsVerbArg);
                            }
                        }
                    }
                    else
                    {
                        if (_settings.LeaveShellOpen)
                        {
                            // Wrap the command in a cmd.exe process
                            info = ShellCommand.SetProcessStartInfo("cmd.exe", workingDirectory, $"/k \"{command}\"", runAsVerbArg);
                        }
                        else
                        {
                            info = ShellCommand.SetProcessStartInfo(command, verb: runAsVerbArg);
                        }
                    }
                }
            }
            else
            {
                throw new NotImplementedException();
            }

            info.UseShellExecute = true;

            _settings.AddCmdHistory(trimmedCommand);

            return info;
        }

        private enum RunAsType
        {
            None,
            Administrator,
            OtherUser,
        }

        private void Execute(Func<ProcessStartInfo, Process> startProcess, ProcessStartInfo info)
        {
            try
            {
                startProcess(info);
            }
            catch (FileNotFoundException e)
            {
                var name = "Plugin: " + Properties.Resources.wox_plugin_cmd_plugin_name;
                var message = $"{Properties.Resources.wox_plugin_cmd_command_not_found}: {e.Message}";
                _context.API.ShowMsg(name, message);
            }
            catch (Win32Exception e)
            {
                var name = "Plugin: " + Properties.Resources.wox_plugin_cmd_plugin_name;
                var message = $"{Properties.Resources.wox_plugin_cmd_command_failed}: {e.Message}";
                _context.API.ShowMsg(name, message);
            }
        }

        private static bool ExistInPath(string filename)
        {
            if (File.Exists(filename))
            {
                return true;
            }
            else
            {
                var values = Environment.GetEnvironmentVariable("PATH");
                if (values != null)
                {
                    foreach (var path in values.Split(';'))
                    {
                        var path1 = Path.Combine(path, filename);
                        var path2 = Path.Combine(path, filename + ".exe");
                        if (File.Exists(path1) || File.Exists(path2))
                        {
                            return true;
                        }
                    }

                    return false;
                }
                else
                {
                    return false;
                }
            }
        }

        public void Init(PluginInitContext context)
        {
            this._context = context;
            _context.API.ThemeChanged += OnThemeChanged;
            UpdateIconPath(_context.API.GetCurrentTheme());
        }

        // Todo : Update with theme based IconPath
        private void UpdateIconPath(Theme theme)
        {
            if (theme == Theme.Light || theme == Theme.HighContrastWhite)
            {
                IconPath = "Images/shell.light.png";
            }
            else
            {
                IconPath = "Images/shell.dark.png";
            }
        }

        private void OnThemeChanged(Theme currentTheme, Theme newTheme)
        {
            UpdateIconPath(newTheme);
        }

        public Control CreateSettingPanel()
        {
            throw new NotImplementedException();
        }

        public string GetTranslatedPluginTitle()
        {
            return Properties.Resources.wox_plugin_cmd_plugin_name;
        }

        public string GetTranslatedPluginDescription()
        {
            return Properties.Resources.wox_plugin_cmd_plugin_description;
        }

        public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
        {
            var resultlist = new List<ContextMenuResult>
            {
                new ContextMenuResult
                {
                    PluginName = Assembly.GetExecutingAssembly().GetName().Name,
                    Title = Properties.Resources.wox_plugin_cmd_run_as_administrator,
                    Glyph = "\xE7EF",
                    FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                    AcceleratorKey = Key.Enter,
                    AcceleratorModifiers = ModifierKeys.Control | ModifierKeys.Shift,
                    Action = c =>
                    {
                        Execute(Process.Start, PrepareProcessStartInfo(selectedResult.Title, RunAsType.Administrator));
                        return true;
                    },
                },
                new ContextMenuResult
                {
                    PluginName = Assembly.GetExecutingAssembly().GetName().Name,
                    Title = Properties.Resources.wox_plugin_cmd_run_as_user,
                    Glyph = "\xE7EE",
                    FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                    AcceleratorKey = Key.U,
                    AcceleratorModifiers = ModifierKeys.Control | ModifierKeys.Shift,
                    Action = _ =>
                    {
                        Execute(Process.Start, PrepareProcessStartInfo(selectedResult.Title, RunAsType.OtherUser));
                        return true;
                    },
                },
            };

            return resultlist;
        }

        public void UpdateSettings(PowerLauncherPluginSettings settings)
        {
            var leaveShellOpen = false;
            var shellOption = 2;

            if (settings != null && settings.AdditionalOptions != null)
            {
                var optionLeaveShellOpen = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "LeaveShellOpen");
                leaveShellOpen = optionLeaveShellOpen?.Value ?? leaveShellOpen;
                _settings.LeaveShellOpen = leaveShellOpen;

                var optionShell = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "ShellCommandExecution");
                shellOption = optionShell?.ComboBoxValue ?? shellOption;
                _settings.Shell = (ExecutionShell)shellOption;
            }

            Save();
        }
    }
}
