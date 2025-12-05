// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;

namespace KeystrokeOverlayUI.Services
{
    /// <summary>
    /// Manages the lifetime of the native keystroke server process.
    /// Starts it automatically when the UI launches and stops it when the UI exits.
    /// </summary>
    public sealed class KeystrokeServerProcessManager : IDisposable
    {
        private Process _serverProcess;

        /// <summary>
        /// Starts the keystroke server EXE if it is not already running.
        /// </summary>
        public void Start()
        {
            if (_serverProcess != null && !_serverProcess.HasExited)
            {
                return;
            }

            string exePath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "PowerToys.KeystrokeOverlayKeystrokeServer.exe");

            if (!File.Exists(exePath))
            {
                Debug.WriteLine($"[KeystrokeServer] EXE not found: {exePath}");
                return;
            }

            Debug.WriteLine($"[KeystrokeServer] Starting: {exePath}");

            _serverProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
                EnableRaisingEvents = true,
            };

            _serverProcess.Exited += (s, e) =>
            {
                Debug.WriteLine("[KeystrokeServer] Process exited.");
            };

            _serverProcess.Start();
        }

        /// <summary>
        /// Stops the native keystroke server if it is running.
        /// </summary>
        public void Stop()
        {
            try
            {
                if (_serverProcess != null && !_serverProcess.HasExited)
                {
                    Debug.WriteLine("[KeystrokeServer] Stopping...");
                    _serverProcess.Kill(true);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[KeystrokeServer] Stop error: {ex}");
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Stop();
        }
    }
}
