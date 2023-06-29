// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Awake.Core.Models
{
    public struct SystemPowerCapabilities
    {
        [MarshalAs(UnmanagedType.U1)]
        public bool PowerButtonPresent;
        [MarshalAs(UnmanagedType.U1)]
        public bool SleepButtonPresent;
        [MarshalAs(UnmanagedType.U1)]
        public bool LidPresent;
        [MarshalAs(UnmanagedType.U1)]
        public bool SystemS1;
        [MarshalAs(UnmanagedType.U1)]
        public bool SystemS2;
        [MarshalAs(UnmanagedType.U1)]
        public bool SystemS3;
        [MarshalAs(UnmanagedType.U1)]
        public bool SystemS4;
        [MarshalAs(UnmanagedType.U1)]
        public bool SystemS5;
        [MarshalAs(UnmanagedType.U1)]
        public bool HiberFilePresent;
        [MarshalAs(UnmanagedType.U1)]
        public bool FullWake;
        [MarshalAs(UnmanagedType.U1)]
        public bool VideoDimPresent;
        [MarshalAs(UnmanagedType.U1)]
        public bool ApmPresent;
        [MarshalAs(UnmanagedType.U1)]
        public bool UpsPresent;
        [MarshalAs(UnmanagedType.U1)]
        public bool ThermalControl;
        [MarshalAs(UnmanagedType.U1)]
        public bool ProcessorThrottle;
        public byte ProcessorMinThrottle;
        public byte ProcessorMaxThrottle;
        [MarshalAs(UnmanagedType.U1)]
        public bool FastSystemS4;
        [MarshalAs(UnmanagedType.U1)]
        public bool Hiberboot;
        [MarshalAs(UnmanagedType.U1)]
        public bool WakeAlarmPresent;
        [MarshalAs(UnmanagedType.U1)]
        public bool AoAc;
        [MarshalAs(UnmanagedType.U1)]
        public bool DiskSpinDown;
        public byte HiberFileType;
        [MarshalAs(UnmanagedType.U1)]
        public bool AoAcConnectivitySupported;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
        private readonly byte[] spare3;
        [MarshalAs(UnmanagedType.U1)]
        public bool SystemBatteriesPresent;
        [MarshalAs(UnmanagedType.U1)]
        public bool BatteriesAreShortTerm;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public BatteryReportingScale[] BatteryScale;
        public SystemPowerState AcOnLineWake;
        public SystemPowerState SoftLidWake;
        public SystemPowerState RtcWake;
        public SystemPowerState MinDeviceWakeState;
        public SystemPowerState DefaultLowLatencyWake;
    }
}
