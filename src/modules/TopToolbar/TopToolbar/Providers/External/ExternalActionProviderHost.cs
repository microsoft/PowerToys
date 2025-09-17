// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace TopToolbar.Providers.External
{
    public sealed class ExternalActionProviderHost : IDisposable
    {
        private Process _process;

        public string ProviderId { get; }

        public string ExecutablePath { get; }

        public ExternalActionProviderHost(string providerId, string executablePath)
        {
            ProviderId = providerId ?? string.Empty;
            ExecutablePath = executablePath ?? string.Empty;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (_process != null && !_process.HasExited)
            {
                return Task.CompletedTask;
            }

            if (string.IsNullOrWhiteSpace(ExecutablePath))
            {
                throw new InvalidOperationException("ExecutablePath must be provided for external providers.");
            }

            _process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ExecutablePath,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
                EnableRaisingEvents = true,
            };

            _process.Start();
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            if (_process != null && !_process.HasExited)
            {
                try
                {
                    _process.Kill();
                }
                catch
                {
                }
            }

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            StopAsync().GetAwaiter().GetResult();
            _process?.Dispose();
        }
    }
}
