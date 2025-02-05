// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Microsoft.CmdPal.Ext.Shell.Helpers;
using Microsoft.CmdPal.Ext.Shell.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Shell.Commands;

internal sealed partial class ExecuteItem : InvokableCommand
{
    private readonly SettingsManager _settings;
    private readonly RunAsType _runas;

    public string Cmd { get; internal set; } = string.Empty;

    private static readonly char[] Separator = [' '];

    public ExecuteItem(string cmd, SettingsManager settings, RunAsType type = RunAsType.None)
    {
        if (type == RunAsType.Administrator)
        {
            Name = Properties.Resources.cmd_run_as_administrator;
            Icon = new IconInfo("\xE7EF"); // Admin Icon
        }
        else if (type == RunAsType.OtherUser)
        {
            Name = Properties.Resources.cmd_run_as_user;
            Icon = new IconInfo("\xE7EE"); // User Icon
        }
        else
        {
            Name = Properties.Resources.generic_run_command;
            Icon = new IconInfo("\uE751"); // Return Key Icon
        }

        Cmd = cmd;
        _settings = settings;
        _runas = type;
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

    private void Execute(Func<ProcessStartInfo, Process?> startProcess, ProcessStartInfo info)
    {
        if (startProcess == null)
        {
            return;
        }

        try
        {
            startProcess(info);
        }
        catch (FileNotFoundException e)
        {
            var name = "Plugin: " + Properties.Resources.cmd_plugin_name;
            var message = $"{Properties.Resources.cmd_command_not_found}: {e.Message}";

            // GH TODO #138 -- show this message once that's wired up
            // _context.API.ShowMsg(name, message);
        }
        catch (Win32Exception e)
        {
            var name = "Plugin: " + Properties.Resources.cmd_plugin_name;
            var message = $"{Properties.Resources.cmd_command_failed}: {e.Message}";
            ExtensionHost.LogMessage(new LogMessage() { Message = name + message });

            // GH TODO #138 -- show this message once that's wired up
            // _context.API.ShowMsg(name, message);
        }
    }

    public static ProcessStartInfo SetProcessStartInfo(string fileName, string workingDirectory = "", string arguments = "", string verb = "")
    {
        var info = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            Arguments = arguments,
            Verb = verb,
        };

        return info;
    }

    private ProcessStartInfo PrepareProcessStartInfo(string command, RunAsType runAs = RunAsType.None)
    {
        command = Environment.ExpandEnvironmentVariables(command);
        var workingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        // Set runAsArg
        var runAsVerbArg = string.Empty;
        if (runAs == RunAsType.OtherUser)
        {
            runAsVerbArg = "runAsUser";
        }
        else if (runAs == RunAsType.Administrator || _settings.RunAsAdministrator)
        {
            runAsVerbArg = "runAs";
        }

        if (Enum.TryParse<ExecutionShell>(_settings.ShellCommandExecution, out var executionShell))
        {
            ProcessStartInfo info;
            if (executionShell == ExecutionShell.Cmd)
            {
                var arguments = _settings.LeaveShellOpen ? $"/k \"{command}\"" : $"/c \"{command}\" & pause";

                info = SetProcessStartInfo("cmd.exe", workingDirectory, arguments, runAsVerbArg);
            }
            else if (executionShell == ExecutionShell.Powershell)
            {
                var arguments = _settings.LeaveShellOpen
                    ? $"-NoExit \"{command}\""
                    : $"\"{command} ; Read-Host -Prompt \\\"{Resources.run_plugin_cmd_wait_message}\\\"\"";
                info = SetProcessStartInfo("powershell.exe", workingDirectory, arguments, runAsVerbArg);
            }
            else if (executionShell == ExecutionShell.PowerShellSeven)
            {
                var arguments = _settings.LeaveShellOpen
                    ? $"-NoExit -C \"{command}\""
                    : $"-C \"{command} ; Read-Host -Prompt \\\"{Resources.run_plugin_cmd_wait_message}\\\"\"";
                info = SetProcessStartInfo("pwsh.exe", workingDirectory, arguments, runAsVerbArg);
            }
            else if (executionShell == ExecutionShell.WindowsTerminalCmd)
            {
                var arguments = _settings.LeaveShellOpen ? $"cmd.exe /k \"{command}\"" : $"cmd.exe /c \"{command}\" & pause";
                info = SetProcessStartInfo("wt.exe", workingDirectory, arguments, runAsVerbArg);
            }
            else if (executionShell == ExecutionShell.WindowsTerminalPowerShell)
            {
                var arguments = _settings.LeaveShellOpen ? $"powershell -NoExit -C \"{command}\"" : $"powershell -C \"{command}\"";
                info = SetProcessStartInfo("wt.exe", workingDirectory, arguments, runAsVerbArg);
            }
            else if (executionShell == ExecutionShell.WindowsTerminalPowerShellSeven)
            {
                var arguments = _settings.LeaveShellOpen ? $"pwsh.exe -NoExit -C \"{command}\"" : $"pwsh.exe -C \"{command}\"";
                info = SetProcessStartInfo("wt.exe", workingDirectory, arguments, runAsVerbArg);
            }
            else if (executionShell == ExecutionShell.RunCommand)
            {
                // Open explorer if the path is a file or directory
                if (Directory.Exists(command) || File.Exists(command))
                {
                    info = SetProcessStartInfo("explorer.exe", arguments: command, verb: runAsVerbArg);
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
                                info = SetProcessStartInfo("cmd.exe", workingDirectory, $"/k \"{filename} {arguments}\"", runAsVerbArg);
                            }
                            else
                            {
                                info = SetProcessStartInfo(filename, workingDirectory, arguments, runAsVerbArg);
                            }
                        }
                        else
                        {
                            if (_settings.LeaveShellOpen)
                            {
                                // Wrap the command in a cmd.exe process
                                info = SetProcessStartInfo("cmd.exe", workingDirectory, $"/k \"{command}\"", runAsVerbArg);
                            }
                            else
                            {
                                info = SetProcessStartInfo(command, verb: runAsVerbArg);
                            }
                        }
                    }
                    else
                    {
                        if (_settings.LeaveShellOpen)
                        {
                            // Wrap the command in a cmd.exe process
                            info = SetProcessStartInfo("cmd.exe", workingDirectory, $"/k \"{command}\"", runAsVerbArg);
                        }
                        else
                        {
                            info = SetProcessStartInfo(command, verb: runAsVerbArg);
                        }
                    }
                }
            }
            else
            {
                throw new NotImplementedException();
            }

            info.UseShellExecute = true;

            _settings.AddCmdHistory(command);

            return info;
        }
        else
        {
            ExtensionHost.LogMessage(new LogMessage() { Message = "Error extracting setting" });
            throw new NotImplementedException();
        }
    }

    public override CommandResult Invoke()
    {
        try
        {
            Execute(Process.Start, PrepareProcessStartInfo(Cmd, _runas));
        }
        catch
        {
            ExtensionHost.LogMessage(new LogMessage() { Message = "Error starting the process " });
        }

        return CommandResult.Dismiss();
    }
}
