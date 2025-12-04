using System;
using System.Diagnostics;
using System.Text;

namespace Community.PowerToys.Run.Plugin.EnhancedShell
{
    public enum ShellType
    {
        CommandPrompt,
        PowerShell
    }

    public class ExecutionResult
    {
        public int ExitCode { get; set; }
        public string Output { get; set; }
        public string Error { get; set; }
        public TimeSpan ExecutionTime { get; set; }
    }

    public class CommandExecutor : IDisposable
    {
        private bool _disposed;

        public ExecutionResult Execute(string command, ShellType shellType, bool runAsAdmin)
        {
            var startTime = DateTime.Now;
            var result = new ExecutionResult();

            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = GetShellExecutable(shellType),
                    Arguments = GetShellArguments(command, shellType),
                    UseShellExecute = runAsAdmin,
                    RedirectStandardOutput = !runAsAdmin,
                    RedirectStandardError = !runAsAdmin,
                    CreateNoWindow = !runAsAdmin,
                    WindowStyle = runAsAdmin ? ProcessWindowStyle.Normal : ProcessWindowStyle.Hidden
                };

                if (runAsAdmin)
                {
                    processInfo.Verb = "runas";
                }

                using (var process = Process.Start(processInfo))
                {
                    if (!runAsAdmin)
                    {
                        var outputBuilder = new StringBuilder();
                        var errorBuilder = new StringBuilder();

                        process.OutputDataReceived += (sender, e) =>
                        {
                            if (e.Data != null) outputBuilder.AppendLine(e.Data);
                        };

                        process.ErrorDataReceived += (sender, e) =>
                        {
                            if (e.Data != null) errorBuilder.AppendLine(e.Data);
                        };

                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();

                        process.WaitForExit();

                        result.Output = outputBuilder.ToString();
                        result.Error = errorBuilder.ToString();
                        result.ExitCode = process.ExitCode;
                    }
                    else
                    {
                        // For admin commands, just track that they started
                        result.ExitCode = 0;
                        result.Output = "Command launched with administrator privileges";
                    }
                }
            }
            catch (Exception ex)
            {
                result.ExitCode = -1;
                result.Error = ex.Message;
            }

            result.ExecutionTime = DateTime.Now - startTime;
            return result;
        }

        private string GetShellExecutable(ShellType shellType)
        {
            return shellType switch
            {
                ShellType.PowerShell => "powershell.exe",
                ShellType.CommandPrompt => "cmd.exe",
                _ => "cmd.exe"
            };
        }

        private string GetShellArguments(string command, ShellType shellType)
        {
            return shellType switch
            {
                ShellType.PowerShell => $"-NoProfile -Command \"{command}\"",
                ShellType.CommandPrompt => $"/c {command}",
                _ => $"/c {command}"
            };
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
        }
    }
}
