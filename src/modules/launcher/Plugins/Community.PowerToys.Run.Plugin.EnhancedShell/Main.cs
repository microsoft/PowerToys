using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using Wox.Plugin;
using Wox.Plugin.Logger;
using ManagedCommon;

namespace Community.PowerToys.Run.Plugin.EnhancedShell
{
    public class Main : IPlugin, IDelayedExecutionPlugin, ISettingProvider, IDisposable
    {
        public static string PluginID => "E7A9D8B3F4C54A2E9B6D1F8C3E5A7B9D";
        
        private PluginInitContext _context;
        private CommandExecutor _executor;
        private CommandHistory _history;
        private CommandValidator _validator;
        private bool _disposed;

        public string Name => "Enhanced Shell";
        public string Description => "Execute CMD and PowerShell commands with history, validation, and autocomplete";

        public void Init(PluginInitContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _executor = new CommandExecutor();
            _history = new CommandHistory(GetHistoryPath());
            _validator = new CommandValidator();
            
            Log.Info("Enhanced Shell Plugin initialized", GetType());
        }

        public List<Result> Query(Query query)
        {
            return Query(query, false);
        }

        public List<Result> Query(Query query, bool delayedExecution)
        {
            var results = new List<Result>();
            
            if (string.IsNullOrWhiteSpace(query.Search))
            {
                // Show recent commands when no query
                results.AddRange(GetRecentCommands());
                results.Add(GetHelpResult());
                return results;
            }

            var searchTerm = query.Search.Trim();
            
            // Validate command
            var validation = _validator.Validate(searchTerm);
            
            // Add execution result
            results.Add(new Result
            {
                Title = $"Execute: {searchTerm}",
                SubTitle = validation.IsValid 
                    ? "Press Enter to execute in CMD (Ctrl+Enter for PowerShell, Ctrl+Shift+Enter for admin)"
                    : $"Warning: {validation.Message}",
                IcoPath = "Images\\shell.light.png",
                Score = 1000,
                Action = context =>
                {
                    var shellType = GetShellType(context);
                    var runAsAdmin = context.SpecialKeyState.CtrlPressed && 
                                   context.SpecialKeyState.ShiftPressed;
                    
                    return ExecuteCommand(searchTerm, shellType, runAsAdmin);
                },
                ContextData = searchTerm
            });

            // Add history matches
            var historyResults = _history.Search(searchTerm)
                .Take(5)
                .Select(cmd => CreateHistoryResult(cmd));
            results.AddRange(historyResults);

            // Add suggestions based on partial input
            if (delayedExecution)
            {
                var suggestions = GetCommandSuggestions(searchTerm);
                results.AddRange(suggestions);
            }

            return results;
        }

        private bool ExecuteCommand(string command, ShellType shellType, bool runAsAdmin)
        {
            try
            {
                var result = _executor.Execute(command, shellType, runAsAdmin);
                
                // Save to history
                _history.Add(new ShellCommand
                {
                    Command = command,
                    ShellType = shellType,
                    ExecutedAt = DateTime.Now,
                    ExitCode = result.ExitCode
                });

                // Show result notification
                if (result.ExitCode == 0)
                {
                    _context.API.ShowNotification(
                        "Command Executed Successfully",
                        result.Output.Length > 100 
                            ? result.Output.Substring(0, 100) + "..." 
                            : result.Output
                    );
                }
                else
                {
                    _context.API.ShowMsg(
                        "Command Failed",
                        $"Exit Code: {result.ExitCode}\n{result.Error}"
                    );
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.Exception("Failed to execute command", ex, GetType());
                _context.API.ShowMsg("Execution Error", ex.Message);
                return false;
            }
        }

        private ShellType GetShellType(ActionContext context)
        {
            // Ctrl+Enter = PowerShell, Enter = CMD
            return context.SpecialKeyState.CtrlPressed 
                ? ShellType.PowerShell 
                : ShellType.CommandPrompt;
        }

        private List<Result> GetRecentCommands()
        {
            return _history.GetRecent(10)
                .Select(CreateHistoryResult)
                .ToList();
        }

        private Result CreateHistoryResult(ShellCommand command)
        {
            var minutesSinceExecution = (DateTime.Now - command.ExecutedAt).TotalMinutes;
            const int baseScore = 900;
            const int minScore = 0;

            return new Result
            {
                Title = command.Command,
                SubTitle = $"Last used: {command.ExecutedAt:g} ({command.ShellType})",
                IcoPath = "Images\\shell.light.png",
                Score = Math.Max(minScore, baseScore - (int)minutesSinceExecution),
                Action = context =>
                {
                    _context.API.ChangeQuery($"{_context.CurrentPluginMetadata.ActionKeyword} {command.Command}");
                    return false;
                },
                ContextData = command
            };
        }

        private Result GetHelpResult()
        {
            return new Result
            {
                Title = "Enhanced Shell Plugin",
                SubTitle = "Enter: CMD | Ctrl+Enter: PowerShell | Ctrl+Shift+Enter: Run as Admin",
                IcoPath = "Images\\shell.light.png",
                Score = 100,
                Action = _ =>
                {
                    _context.API.ShowMsg(
                        "Enhanced Shell Plugin Help",
                        "Type any command to execute.\n" +
                        "• Enter: Execute in Command Prompt\n" +
                        "• Ctrl+Enter: Execute in PowerShell\n" +
                        "• Ctrl+Shift+Enter: Run as Administrator\n\n" +
                        "Recent commands and suggestions appear automatically."
                    );
                    return false;
                }
            };
        }

        private List<Result> GetCommandSuggestions(string searchTerm)
        {
            // Common command suggestions
            var suggestions = new Dictionary<string, string>
            {
                ["ipconfig"] = "Display network configuration",
                ["ping"] = "Test network connectivity",
                ["netstat"] = "Display network statistics",
                ["tasklist"] = "List running processes",
                ["taskkill"] = "Terminate processes",
                ["dir"] = "List directory contents",
                ["cd"] = "Change directory",
                ["mkdir"] = "Create directory",
                ["del"] = "Delete files",
                ["copy"] = "Copy files",
                ["move"] = "Move files",
                ["Get-Process"] = "PowerShell: List processes",
                ["Get-Service"] = "PowerShell: List services",
                ["Get-ChildItem"] = "PowerShell: List directory",
                ["Get-Help"] = "PowerShell: Get help"
            };

            return suggestions
                .Where(kvp => kvp.Key.StartsWith(searchTerm, StringComparison.OrdinalIgnoreCase))
                .Select(kvp => new Result
                {
                    Title = kvp.Key,
                    SubTitle = kvp.Value,
                    IcoPath = "Images\\shell.light.png",
                    Score = 500,
                    Action = _ =>
                    {
                        _context.API.ChangeQuery($"{_context.CurrentPluginMetadata.ActionKeyword} {kvp.Key} ");
                        return false;
                    }
                })
                .ToList();
        }

        private string GetHistoryPath()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var pluginPath = System.IO.Path.Combine(
                localAppData, 
                "Microsoft", 
                "PowerToys", 
                "PowerToys Run", 
                "Plugins", 
                "EnhancedShell"
            );
            
            System.IO.Directory.CreateDirectory(pluginPath);
            return System.IO.Path.Combine(pluginPath, "history.db");
        }

        // ISettingProvider implementation
        public Control CreateSettingPanel()
        {
            throw new NotImplementedException();
        }

        public void UpdateSettings(PowerLauncherPluginSettings settings)
        {
            // Handle settings updates
        }

        public IEnumerable<PluginAdditionalOption> AdditionalOptions => new List<PluginAdditionalOption>
        {
            new PluginAdditionalOption
            {
                Key = "DefaultShell",
                DisplayLabel = "Default Shell",
                DisplayDescription = "Choose default shell for command execution",
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Combobox,
                ComboBoxItems = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("CMD", "Command Prompt"),
                    new KeyValuePair<string, string>("PowerShell", "PowerShell")
                },
                ComboBoxValue = 0
            },
            new PluginAdditionalOption
            {
                Key = "KeepShellOpen",
                DisplayLabel = "Keep Shell Open",
                DisplayDescription = "Keep command window open after execution",
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Checkbox,
                Value = false
            },
            new PluginAdditionalOption
            {
                Key = "SaveHistory",
                DisplayLabel = "Save Command History",
                DisplayDescription = "Save executed commands for quick access",
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Checkbox,
                Value = true
            }
        };

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                _history?.Dispose();
                _executor?.Dispose();
            }

            _disposed = true;
        }
    }
}
