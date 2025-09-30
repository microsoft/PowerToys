// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TopToolbar.Logging;

namespace TopToolbar.Providers.External
{
    public sealed class ExternalActionProviderHost : IDisposable
    {
        private readonly object _sync = new();
        private readonly string _providerId;
        private readonly string _executablePath;
        private readonly string _arguments;
        private readonly string _workingDirectory;
        private readonly IReadOnlyDictionary<string, string> _environment;
        private readonly TimeSpan _shutdownTimeout;

        private Process _process;
        private StreamWriter _stdin;
        private StreamReader _stdout;
        private StreamReader _stderr;
        private CancellationTokenSource _stderrCts;
        private Task _stderrPump;
        private bool _disposed;

        public ExternalActionProviderHost(
            string providerId,
            string executablePath,
            string arguments = "",
            string workingDirectory = "",
            IReadOnlyDictionary<string, string> environment = null,
            TimeSpan? shutdownTimeout = null)
        {
            _providerId = providerId ?? string.Empty;
            _executablePath = ResolveExecutable(executablePath);
            _arguments = arguments ?? string.Empty;
            _workingDirectory = ResolveWorkingDirectory(workingDirectory, _executablePath);
            _environment = environment ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _shutdownTimeout = shutdownTimeout ?? TimeSpan.FromSeconds(3);
        }

        public bool IsRunning
        {
            get
            {
                lock (_sync)
                {
                    return _process != null && !_process.HasExited;
                }
            }
        }

        public StreamWriter StandardInput
        {
            get
            {
                lock (_sync)
                {
                    return _stdin ?? throw new InvalidOperationException("External provider host is not started.");
                }
            }
        }

        public StreamReader StandardOutput
        {
            get
            {
                lock (_sync)
                {
                    return _stdout ?? throw new InvalidOperationException("External provider host is not started.");
                }
            }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                ThrowIfDisposed();

                if (_process != null && !_process.HasExited)
                {
                    return Task.CompletedTask;
                }

                CleanupResources();

                var startInfo = new ProcessStartInfo
                {
                    FileName = _executablePath,
                    Arguments = _arguments,
                    WorkingDirectory = _workingDirectory,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                    CreateNoWindow = true,
                };

                foreach (var pair in _environment)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key))
                    {
                        continue;
                    }

                    startInfo.Environment[pair.Key] = pair.Value ?? string.Empty;
                }

                _process = new Process
                {
                    StartInfo = startInfo,
                    EnableRaisingEvents = true,
                };

                _process.Exited += OnProcessExited;

                try
                {
                    if (!_process.Start())
                    {
                        throw new InvalidOperationException($"Failed to start external provider '{_providerId}'.");
                    }
                }
                catch
                {
                    _process.Exited -= OnProcessExited;
                    _process.Dispose();
                    _process = null;
                    throw;
                }

                _stdin = _process.StandardInput;
                _stdin.AutoFlush = true;
                _stdout = _process.StandardOutput;
                _stderr = _process.StandardError;

                _stderrCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _stderrPump = Task.Run(() => PumpStandardErrorAsync(_stderrCts.Token), CancellationToken.None);

                if (_process.HasExited)
                {
                    throw new InvalidOperationException($"External provider '{_providerId}' exited immediately with code {_process.ExitCode}.");
                }
            }

            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            lock (_sync)
            {
                if (_process == null)
                {
                    return Task.CompletedTask;
                }
            }

            return Task.Run(() => ShutdownProcess(killIfNeeded: false));
        }

        private void OnProcessExited(object sender, EventArgs e)
        {
            lock (_sync)
            {
                AppLogger.LogInfo($"ExternalActionProviderHost: provider '{_providerId}' exited with code {_process?.ExitCode ?? -1}.");
                CleanupResources();
            }
        }

        private void ShutdownProcess(bool killIfNeeded)
        {
            Process process;
            StreamWriter stdin;
            StreamReader stdout;
            StreamReader stderr;
            CancellationTokenSource stderrCts;
            Task stderrPump;

            lock (_sync)
            {
                process = _process;
                stdin = _stdin;
                stdout = _stdout;
                stderr = _stderr;
                stderrCts = _stderrCts;
                stderrPump = _stderrPump;

                _process = null;
                _stdin = null;
                _stdout = null;
                _stderr = null;
                _stderrCts = null;
                _stderrPump = null;
            }

            try
            {
                stderrCts?.Cancel();
                stderr?.Close();
                stdout?.Close();

                if (stdin != null)
                {
                    try
                    {
                        stdin.Close();
                    }
                    catch
                    {
                        // Ignore errors while closing input
                    }
                }

                if (stderrPump != null)
                {
                    try
                    {
                        stderrPump.Wait(_shutdownTimeout);
                    }
                    catch
                    {
                    }
                }

                if (process != null && !process.HasExited)
                {
                    var exited = process.WaitForExit((int)_shutdownTimeout.TotalMilliseconds);
                    if (!exited && killIfNeeded)
                    {
                        try
                        {
                            process.Kill(true);
                            exited = true;
                        }
                        catch
                        {
                        }
                    }

                    if (killIfNeeded && !process.HasExited)
                    {
                        process.WaitForExit();
                    }
                }
            }
            finally
            {
                stderrCts?.Dispose();
                stderrPump?.Dispose();
                process?.Dispose();
                stdin?.Dispose();
                stdout?.Dispose();
                stderr?.Dispose();
            }
        }

        private async Task PumpStandardErrorAsync(CancellationToken token)
        {
            try
            {
                StreamReader reader;
                lock (_sync)
                {
                    reader = _stderr;
                }

                if (reader == null)
                {
                    return;
                }

                while (!token.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync(token).ConfigureAwait(false);
                    if (line == null)
                    {
                        break;
                    }

                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        AppLogger.LogInfo($"ExternalActionProviderHost[{_providerId}]: {line}");
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                // Expected when shutting down
            }
            catch (Exception ex)
            {
                AppLogger.LogWarning($"ExternalActionProviderHost: stderr pump for '{_providerId}' failed - {ex.Message}.");
            }
        }

        private void CleanupResources()
        {
            _stdin?.Dispose();
            _stdout?.Dispose();
            _stderr?.Dispose();
            _stderrCts?.Cancel();
            _stderrCts?.Dispose();
            _stderrPump?.Dispose();

            _stdin = null;
            _stdout = null;
            _stderr = null;
            _stderrCts = null;
            _stderrPump = null;
        }

        private static string ResolveExecutable(string executablePath)
        {
            var raw = executablePath?.Trim();
            if (string.IsNullOrWhiteSpace(raw))
            {
                throw new InvalidOperationException("Executable path must be provided for external providers.");
            }

            var expanded = Environment.ExpandEnvironmentVariables(raw);
            if (Path.IsPathRooted(expanded) && File.Exists(expanded))
            {
                return expanded;
            }

            var baseDir = AppContext.BaseDirectory;
            if (!string.IsNullOrEmpty(baseDir))
            {
                var candidate = Path.GetFullPath(Path.Combine(baseDir, expanded));
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            if (File.Exists(expanded))
            {
                return Path.GetFullPath(expanded);
            }

            throw new FileNotFoundException($"External provider executable '{raw}' was not found.", expanded);
        }

        private static string ResolveWorkingDirectory(string workingDirectory, string executablePath)
        {
            var raw = workingDirectory?.Trim();
            if (!string.IsNullOrWhiteSpace(raw))
            {
                var expanded = Environment.ExpandEnvironmentVariables(raw);
                return Path.IsPathRooted(expanded)
                    ? expanded
                    : Path.GetFullPath(expanded);
            }

            return Path.GetDirectoryName(executablePath) ?? Environment.CurrentDirectory;
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, nameof(ExternalActionProviderHost));
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            ShutdownProcess(killIfNeeded: true);
            GC.SuppressFinalize(this);
        }
    }
}
