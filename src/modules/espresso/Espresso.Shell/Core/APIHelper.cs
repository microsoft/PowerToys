using System;
using System.Runtime.InteropServices;

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
                bool stateSettingSucceeded = (SetThreadExecutionState(state) != 0);
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

        public static bool SetTimedKeepAwake(long seconds, bool keepDisplayOn = true)
        {
            if (keepDisplayOn)
            {
                var success = SetAwakeState(EXECUTION_STATE.ES_SYSTEM_REQUIRED | EXECUTION_STATE.ES_DISPLAY_REQUIRED | EXECUTION_STATE.ES_CONTINUOUS);
                if (success)
                {
                    RunTimedLoop(seconds);
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                var success = SetAwakeState(EXECUTION_STATE.ES_SYSTEM_REQUIRED | EXECUTION_STATE.ES_CONTINUOUS);
                if (success)
                {
                    RunTimedLoop(seconds);
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        private static void RunTimedLoop(long seconds)
        {
            var startTime = DateTime.UtcNow;
            while (DateTime.UtcNow - startTime < TimeSpan.FromSeconds(seconds))
            {
                // We do nothing.
            }
        }
    }
}
