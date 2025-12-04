using System;
using System.Collections.Generic;
using System.Linq;

namespace Community.PowerToys.Run.Plugin.EnhancedShell
{
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string Message { get; set; }
        public ValidationSeverity Severity { get; set; }
    }

    public enum ValidationSeverity
    {
        Info,
        Warning,
        Error
    }

    public class CommandValidator
    {
        private readonly HashSet<string> _dangerousCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "format", "del /s", "rmdir /s", "rd /s", "deltree",
            "Remove-Item -Recurse", "Remove-Item -Force"
        };

        private readonly HashSet<string> _validCmdCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "dir", "cd", "copy", "move", "del", "mkdir", "rmdir", "type", "echo",
            "ping", "ipconfig", "netstat", "tasklist", "taskkill", "systeminfo",
            "chkdsk", "diskpart", "sfc", "shutdown", "cls", "exit", "help"
        };

        public ValidationResult Validate(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                return new ValidationResult
                {
                    IsValid = false,
                    Message = "Command cannot be empty",
                    Severity = ValidationSeverity.Error
                };
            }

            // Check for dangerous commands
            foreach (var dangerous in _dangerousCommands)
            {
                if (command.Contains(dangerous, StringComparison.OrdinalIgnoreCase))
                {
                    return new ValidationResult
                    {
                        IsValid = true,
                        Message = "This command may be destructive. Use with caution!",
                        Severity = ValidationSeverity.Warning
                    };
                }
            }

            // Check if it's a valid command
            var firstWord = command.Split(' ')[0].ToLower();
            
            if (_validCmdCommands.Contains(firstWord) || 
                firstWord.StartsWith("get-", StringComparison.OrdinalIgnoreCase) ||
                firstWord.StartsWith("set-", StringComparison.OrdinalIgnoreCase))
            {
                return new ValidationResult
                {
                    IsValid = true,
                    Message = "Valid command",
                    Severity = ValidationSeverity.Info
                };
            }

            return new ValidationResult
            {
                IsValid = true,
                Message = "Unknown command - will attempt to execute",
                Severity = ValidationSeverity.Info
            };
        }
    }
}
