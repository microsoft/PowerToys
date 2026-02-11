// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using ManagedCommon;

namespace RunnerV2.Helpers
{
    internal static class QuickAccessHelper
    {
        private static Process _process = new Process();
        private static EventWaitHandle _showEvent = new(false, EventResetMode.AutoReset, "Local\\PowerToysQuickAccess__Show");
        private static EventWaitHandle _exitEvent = new(false, EventResetMode.AutoReset, "Local\\PowerToysQuickAccess__Exit");

        public static void Start()
        {
            if (IsRunning)
            {
                return;
            }

            Logger.LogInfo("Starting Quick Access");

            lock (_process)
            {
                string runnerPipeName = "\\\\.\\pipe\\powertoys_quick_access_runner_" + Guid.NewGuid();
                string appPipeName = "\\\\.\\pipe\\powertoys_quick_access_ui_" + Guid.NewGuid();

                PowerToys.Interop.TwoWayPipeMessageIPCManaged quickAccessIpc = new(runnerPipeName, appPipeName, SettingsHelper.OnSettingsMessageReceived);

                string exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WinUI3Apps\\PowerToys.QuickAccess.exe");

                if (!File.Exists(exePath))
                {
                    Logger.LogError($"Quick Access executable not found at path: {exePath}");
                    return;
                }

                _process = new Process();
                _process.StartInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = $"--show-event=\"Local\\PowerToysQuickAccess__Show\" --exit-event=\"Local\\PowerToysQuickAccess__Exit\" --runner-pipe=\"{runnerPipeName}\" --app-pipe=\"{appPipeName}\"",
                };
                _process.Start();
                quickAccessIpc.Start();
            }
        }

        public static bool IsRunning
        {
            get
            {
                try
                {
                    return !_process.HasExited;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        public static void Show()
        {
            Start();

            _showEvent.Set();
        }

        public static void Stop()
        {
            if (!IsRunning)
            {
                return;
            }

            Logger.LogInfo("Stopping Quick Access");
            _exitEvent.Set();
            lock (_process)
            {
                if (!_process.HasExited)
                {
                    _process.WaitForExit(2000);
                }

                if (!_process.HasExited)
                {
                    _process.Kill();
                }
            }
        }
    }
}
