// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using static ManagedCommon.NativeMethods;

namespace ManagedCommon
{
    public partial class InteropEvent : IDisposable
    {
        public const string AlwaysOnTopPin = "Local\\AlwaysOnTopPinEvent-892e0aa2-cfa8-4cc4-b196-ddeb32314ce8";
        public const string AlwaysOnTopTerminate = "Local\\AlwaysOnTopTerminateEvent-cfdf1eae-791f-4953-8021-2f18f3837eae";
        public const string AwakeTerminate = "Local\\PowerToysAwakeExitEvent-c0d5e305-35fc-4fb5-83ec-f6070cfaf7fe";
        public const string SettingsTerminate = "Local\\PowerToysRunnerTerminateSettingsEvent-c34cb661-2e69-4613-a1f8-4e39c25d7ef6";

        private IntPtr _eventHandle;

        public InteropEvent(string eventName)
        {
            _eventHandle = CreateEventW(IntPtr.Zero, false, false, eventName);
        }

        public void Fire()
        {
            if (_eventHandle != IntPtr.Zero)
            {
                SetEvent(_eventHandle);
            }
        }

        ~InteropEvent()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (_eventHandle != IntPtr.Zero)
            {
                CloseHandle(_eventHandle);
                _eventHandle = IntPtr.Zero;
            }

            GC.SuppressFinalize(this);
        }
    }
}
