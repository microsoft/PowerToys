// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using static RunnerV2.NativeMethods;

namespace RunnerV2.Helpers
{
    internal static partial class ElevationHelper
    {
        internal static RestartScheduledMode RestartScheduled { get; set; } = RestartScheduledMode.None;

        internal enum RestartScheduledMode
        {
            None,
            RestartElevated,
            RestartElevatedWithOpenSettings,
            RestartNonElevated,
        }

        private static bool? _cachedValue;

        internal static void RestartIfScheudled()
        {
            switch (RestartScheduled)
            {
                case RestartScheduledMode.None:
                    return;
                case RestartScheduledMode.RestartElevated:
                    RestartAsAdministrator("--restartedElevated");
                    break;
                case RestartScheduledMode.RestartElevatedWithOpenSettings:
                    RestartAsAdministrator("--restartedElevated --open-settings");
                    break;
                case RestartScheduledMode.RestartNonElevated:
                    // Todo: restart unelevated
                    break;
            }
        }

        private static void RestartAsAdministrator(string arguments)
        {
            ProcessStartInfo processStartInfo = new()
            {
                Arguments = arguments,
                Verb = "runas",
                UseShellExecute = true,
                FileName = Environment.ProcessPath,
            };

            try
            {
                Process.Start(processStartInfo);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to restart as administrator: " + ex);
            }
        }

        internal static bool IsProcessElevated(bool useCachedValue = true)
        {
            if (_cachedValue is not null && useCachedValue)
            {
                return _cachedValue.Value;
            }

            bool elevated = false;
            if (OpenProcessToken(Process.GetCurrentProcess().Handle, TOKENQUERY, out nint token))
            {
                TokenElevation elevation = default;
                if (GetTokenInformation(token, TOKEN_INFORMATION_CLASS.TOKEN_ELEVATION, ref elevation, (uint)Marshal.SizeOf(elevation), out uint _))
                {
                    elevated = elevation.TokenIsElevated != 0;
                }

                if (token != IntPtr.Zero)
                {
                    CloseHandle(token);
                }
            }

            _cachedValue = elevated;
            return elevated;
        }
    }
}
