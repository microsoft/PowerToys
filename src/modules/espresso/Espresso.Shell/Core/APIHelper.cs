// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Espresso.Shell.Core
{
    [FlagsAttribute]
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
        private static CancellationTokenSource TokenSource = new CancellationTokenSource();
        private static CancellationToken ThreadToken;

        // More details about the API used: https://docs.microsoft.com/windows/win32/api/winbase/nf-winbase-setthreadexecutionstate
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);

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
                Console.WriteLine($"State setting result:  {stateResult}");

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
            bool success;

            // In case cancellation was already requested.
            //ThreadToken.ThrowIfCancellationRequested();

            if (keepDisplayOn)
            {
                success = SetAwakeState(EXECUTION_STATE.ES_SYSTEM_REQUIRED | EXECUTION_STATE.ES_DISPLAY_REQUIRED | EXECUTION_STATE.ES_CONTINUOUS);
                if (success)
                {
                    Console.WriteLine("Timed keep-awake with display on.");
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
                    Console.WriteLine("Timed keep-awake with display off.");
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
    }
}
