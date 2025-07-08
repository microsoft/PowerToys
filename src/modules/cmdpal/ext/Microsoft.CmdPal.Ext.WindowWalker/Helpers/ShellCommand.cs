using System;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Microsoft.CmdPal.Ext.WindowWalker.Helpers
{
    public static class ShellCommand
    {
        private static readonly Regex SafeInputPattern = new Regex(@"^[a-zA-Z0-9\s\-_\.\\:]+$", RegexOptions.Compiled);
        
        public static void Execute(string command, string arguments = "")
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                throw new ArgumentException("Command cannot be null or empty", nameof(command));
            }

            // Validate command to prevent injection
            if (!IsValidCommand(command))
            {
                throw new ArgumentException("Invalid command format", nameof(command));
            }

            // Validate arguments to prevent injection
            if (!string.IsNullOrEmpty(arguments) && !IsValidArguments(arguments))
            {
                throw new ArgumentException("Invalid arguments format", nameof(arguments));
            }

            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var process = Process.Start(processStartInfo))
                {
                    process?.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                // Log the exception appropriately
                System.Diagnostics.Debug.WriteLine($"Failed to execute command: {ex.Message}");
                throw;
            }
        }

        private static bool IsValidCommand(string command)
        {
            // Allow only safe characters and common executable extensions
            return SafeInputPattern.IsMatch(command) && 
                   (command.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || 
                    !command.Contains("."));
        }

        private static bool IsValidArguments(string arguments)
        {
            // Prevent common injection patterns
            if (arguments.Contains("&") || arguments.Contains("|") || 
                arguments.Contains(";") || arguments.Contains(">") || 
                arguments.Contains("<") || arguments.Contains("$") ||
                arguments.Contains("`") || arguments.Contains("'") ||
                arguments.Contains("\""))
            {
                return false;
            }

            return SafeInputPattern.IsMatch(arguments);
        }
    }
}
