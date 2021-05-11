// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Win32;
using NLog;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Espresso.Shell.Core
{
    [Flags]
    public enum EXECUTION_STATE : uint
    {
        ES_AWAYMODE_REQUIRED = 0x00000040,
        ES_CONTINUOUS = 0x80000000,
        ES_DISPLAY_REQUIRED = 0x00000002,
        ES_SYSTEM_REQUIRED = 0x00000001
    }

    /// <summary>
    /// Helper class that allows talking to Win32 APIs without having to rely on PInvoke in other parts
    /// of the codebase.
    /// </summary>
    public class APIHelper
    {
        private const string BUILD_REGISTRY_LOCATION = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";

        private static CancellationTokenSource TokenSource = new CancellationTokenSource();
        private static CancellationToken ThreadToken;

        private static readonly Logger log;

        // More details about the API used: https://docs.microsoft.com/windows/win32/api/winbase/nf-winbase-setthreadexecutionstate
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);

        [DllImport("Shell32.dll", EntryPoint = "ExtractIconExW", CharSet = CharSet.Unicode, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        private static extern int ExtractIconEx(string sFile, int iIndex, out IntPtr piLargeVersion, out IntPtr piSmallVersion, int amountIcons);

        static APIHelper()
        {
            log = LogManager.GetCurrentClassLogger();
        }

        /// <summary>
        /// Sets the computer awake state using the native Win32 SetThreadExecutionState API. This
        /// function is just a nice-to-have wrapper that helps avoid tracking the success or failure of
        /// the call.
        /// </summary>
        /// <param name="state">Single or multiple EXECUTION_STATE entries.</param>
        /// <returns>true if successful, false if failed</returns>
        private static bool SetAwakeState(EXECUTION_STATE state)
        {
            try
            {
                var stateResult = SetThreadExecutionState(state);
                bool stateSettingSucceeded = (stateResult != 0);

                if (stateSettingSucceeded)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool SetNormalKeepAwake()
        {
            TokenSource.Cancel();
            return SetAwakeState(EXECUTION_STATE.ES_CONTINUOUS);
        }

        public static bool SetIndefiniteKeepAwake(bool keepDisplayOn = true)
        {
            if (keepDisplayOn)
            {
                return SetAwakeState(EXECUTION_STATE.ES_SYSTEM_REQUIRED | EXECUTION_STATE.ES_DISPLAY_REQUIRED | EXECUTION_STATE.ES_CONTINUOUS);
            }
            else
            {
                return SetAwakeState(EXECUTION_STATE.ES_SYSTEM_REQUIRED | EXECUTION_STATE.ES_CONTINUOUS);
            }    
        }

        public static void SetTimedKeepAwake(long seconds, Action<bool> callback, Action failureCallback, bool keepDisplayOn = true)
        {
            ThreadToken = TokenSource.Token;

            Task.Run(() => RunTimedLoop(seconds, keepDisplayOn), ThreadToken)
                .ContinueWith((result) => callback(result.Result), TaskContinuationOptions.OnlyOnRanToCompletion)
                .ContinueWith((result) => failureCallback, TaskContinuationOptions.NotOnRanToCompletion); ;

        }

        private static bool RunTimedLoop(long seconds, bool keepDisplayOn = true)
        {
            bool success = false;

            // In case cancellation was already requested.
            //ThreadToken.ThrowIfCancellationRequested();
            try
            {
                if (keepDisplayOn)
                {
                    success = SetAwakeState(EXECUTION_STATE.ES_SYSTEM_REQUIRED | EXECUTION_STATE.ES_DISPLAY_REQUIRED | EXECUTION_STATE.ES_CONTINUOUS);
                    if (success)
                    {
                        log.Info("Timed keep-awake with display on.");
                        var startTime = DateTime.UtcNow;
                        while (DateTime.UtcNow - startTime < TimeSpan.FromSeconds(seconds))
                        {
                            if (ThreadToken.IsCancellationRequested)
                            {
                                ThreadToken.ThrowIfCancellationRequested();
                            }
                        }
                        return success;
                    }
                    else
                    {
                        return success;
                    }
                }
                else
                {
                    success = SetAwakeState(EXECUTION_STATE.ES_SYSTEM_REQUIRED | EXECUTION_STATE.ES_CONTINUOUS);
                    if (success)
                    {
                        log.Info("Timed keep-awake with display off.");
                        var startTime = DateTime.UtcNow;
                        while (DateTime.UtcNow - startTime < TimeSpan.FromSeconds(seconds))
                        {
                            if (ThreadToken.IsCancellationRequested)
                            {
                                ThreadToken.ThrowIfCancellationRequested();
                            }
                        }
                        return success;
                    }
                    else
                    {
                        return success;
                    }
                }
            }
            catch (OperationCanceledException ex)
            {
                // Task was clearly cancelled.
                log.Debug($"Background thread termination. Message: {ex.Message}");
                return success;
            }
        }

        public static string GetOperatingSystemBuild()
        {
            try
            {
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
                RegistryKey registryKey = Registry.LocalMachine.OpenSubKey(BUILD_REGISTRY_LOCATION);
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.

                if (registryKey != null)
                {
                    var versionString = $"{registryKey.GetValue("ProductName")} {registryKey.GetValue("DisplayVersion")} {registryKey.GetValue("BuildLabEx")}";
                    return versionString;
                }
                else
                {
                    log.Debug("Registry key acquisition for OS failed.");
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                log.Debug($"Could not get registry key for the build number. Error: {ex.Message}");
                return string.Empty;
            }
        }


        public static Icon? Extract(string file, int number, bool largeIcon)
        {
            ExtractIconEx(file, number, out IntPtr large, out IntPtr small, 1);
            try
            {
                return Icon.FromHandle(largeIcon ? large : small);
            }
            catch
            {
                return null;
            }

        }
        
    }
}
