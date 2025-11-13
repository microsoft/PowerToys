// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace PowerDisplay.Native
{
    /// <summary>
    /// Windows API constant definitions
    /// </summary>
    public static class NativeConstants
    {
        /// <summary>
        /// VCP code: Brightness (primary usage)
        /// </summary>
        public const byte VcpCodeBrightness = 0x10;

        /// <summary>
        /// VCP code: Contrast
        /// </summary>
        public const byte VcpCodeContrast = 0x12;

        /// <summary>
        /// VCP code: Backlight control (alternative brightness)
        /// </summary>
        public const byte VcpCodeBacklightControl = 0x13;

        /// <summary>
        /// VCP code: White backlight level
        /// </summary>
        public const byte VcpCodeBacklightLevelWhite = 0x6B;

        /// <summary>
        /// VCP code: Audio speaker volume
        /// </summary>
        public const byte VcpCodeVolume = 0x62;

        /// <summary>
        /// VCP code: Audio mute
        /// </summary>
        public const byte VcpCodeMute = 0x8D;

        /// <summary>
        /// VCP code: Color Temperature Request (0x0C)
        /// Per MCCS v2.2a specification:
        /// - Used to SET and GET specific color temperature presets
        /// - Typically supports discrete values (e.g., 5000K, 6500K, 9300K)
        /// - Primary method for color temperature control
        /// </summary>
        public const byte VcpCodeColorTemperature = 0x0C;

        /// <summary>
        /// VCP code: Color Temperature Increment (0x0B)
        /// Per MCCS v2.2a specification:
        /// - Used for incremental color temperature adjustment
        /// - Typically supports continuous range (0-100 or custom range)
        /// - Fallback method when 0x0C is not supported
        /// </summary>
        public const byte VcpCodeColorTemperatureIncrement = 0x0B;

        /// <summary>
        /// VCP code: Gamma correction (gamma adjustment)
        /// </summary>
        public const byte VcpCodeGamma = 0x72;

        /// <summary>
        /// VCP code: Select color preset (color preset selection)
        /// </summary>
        public const byte VcpCodeSelectColorPreset = 0x14;

        /// <summary>
        /// VCP code: VCP version
        /// </summary>
        public const byte VcpCodeVcpVersion = 0xDF;

        /// <summary>
        /// VCP code: New control value
        /// </summary>
        public const byte VcpCodeNewControlValue = 0x02;

        /// <summary>
        /// Display device attached to desktop
        /// </summary>
        public const uint DisplayDeviceAttachedToDesktop = 0x00000001;

        /// <summary>
        /// Multi-monitor primary display
        /// </summary>
        public const uint DisplayDeviceMultiDriver = 0x00000002;

        /// <summary>
        /// Primary device
        /// </summary>
        public const uint DisplayDevicePrimaryDevice = 0x00000004;

        /// <summary>
        /// Mirroring driver
        /// </summary>
        public const uint DisplayDeviceMirroringDriver = 0x00000008;

        /// <summary>
        /// VGA compatible
        /// </summary>
        public const uint DisplayDeviceVgaCompatible = 0x00000010;

        /// <summary>
        /// Removable device
        /// </summary>
        public const uint DisplayDeviceRemovable = 0x00000020;

        /// <summary>
        /// Get device interface name
        /// </summary>
        public const uint EddGetDeviceInterfaceName = 0x00000001;

        /// <summary>
        /// Primary monitor
        /// </summary>
        public const uint MonitorinfoFPrimary = 0x00000001;

        /// <summary>
        /// Query display config: only active paths
        /// </summary>
        public const uint QdcOnlyActivePaths = 0x00000002;

        /// <summary>
        /// Query display config: all paths
        /// </summary>
        public const uint QdcAllPaths = 0x00000001;

        /// <summary>
        /// Set display config: apply
        /// </summary>
        public const uint SdcApply = 0x00000080;

        /// <summary>
        /// Set display config: use supplied display config
        /// </summary>
        public const uint SdcUseSuppliedDisplayConfig = 0x00000020;

        /// <summary>
        /// Set display config: save to database
        /// </summary>
        public const uint SdcSaveToDatabase = 0x00000200;

        /// <summary>
        /// Set display config: topology supplied
        /// </summary>
        public const uint SdcTopologySupplied = 0x00000010;

        /// <summary>
        /// Set display config: allow path order changes
        /// </summary>
        public const uint SdcAllowPathOrderChanges = 0x00002000;

        /// <summary>
        /// Get target name
        /// </summary>
        public const uint DisplayconfigDeviceInfoGetTargetName = 1;

        /// <summary>
        /// Get SDR white level
        /// </summary>
        public const uint DisplayconfigDeviceInfoGetSdrWhiteLevel = 7;

        /// <summary>
        /// Get advanced color information
        /// </summary>
        public const uint DisplayconfigDeviceInfoGetAdvancedColorInfo = 9;

        /// <summary>
        /// Set SDR white level (custom)
        /// </summary>
        public const uint DisplayconfigDeviceInfoSetSdrWhiteLevel = 0xFFFFFFEE;

        /// <summary>
        /// Path active
        /// </summary>
        public const uint DisplayconfigPathActive = 0x00000001;

        /// <summary>
        /// Path mode index invalid
        /// </summary>
        public const uint DisplayconfigPathModeIdxInvalid = 0xFFFFFFFF;

        /// <summary>
        /// COM initialization: multithreaded
        /// </summary>
        public const uint CoinitMultithreaded = 0x0;

        /// <summary>
        /// RPC authentication level: connect
        /// </summary>
        public const uint RpcCAuthnLevelConnect = 2;

        /// <summary>
        /// RPC impersonation level: impersonate
        /// </summary>
        public const uint RpcCImpLevelImpersonate = 3;

        /// <summary>
        /// RPC authentication service: Win NT
        /// </summary>
        public const uint RpcCAuthnWinnt = 10;

        /// <summary>
        /// RPC authorization service: none
        /// </summary>
        public const uint RpcCAuthzNone = 0;

        /// <summary>
        /// RPC authentication level: call
        /// </summary>
        public const uint RpcCAuthnLevelCall = 3;

        /// <summary>
        /// EOAC: none
        /// </summary>
        public const uint EoacNone = 0;

        /// <summary>
        /// WMI flag: forward only
        /// </summary>
        public const long WbemFlagForwardOnly = 0x20;

        /// <summary>
        /// WMI flag: return immediately
        /// </summary>
        public const long WbemFlagReturnImmediately = 0x10;

        /// <summary>
        /// WMI flag: connect use max wait
        /// </summary>
        public const long WbemFlagConnectUseMaxWait = 0x80;

        /// <summary>
        /// Success
        /// </summary>
        public const int ErrorSuccess = 0;

        /// <summary>
        /// Insufficient buffer
        /// </summary>
        public const int ErrorInsufficientBuffer = 122;

        /// <summary>
        /// Invalid parameter
        /// </summary>
        public const int ErrorInvalidParameter = 87;

        /// <summary>
        /// Access denied
        /// </summary>
        public const int ErrorAccessDenied = 5;

        /// <summary>
        /// General failure
        /// </summary>
        public const int ErrorGenFailure = 31;

        /// <summary>
        /// Unsupported VCP code
        /// </summary>
        public const int ErrorGraphicsDdcciVcpNotSupported = -1071243251;

        /// <summary>
        /// Infinite wait
        /// </summary>
        public const uint Infinite = 0xFFFFFFFF;

        /// <summary>
        /// User message
        /// </summary>
        public const uint WmUser = 0x0400;

        /// <summary>
        /// Output technology: HDMI
        /// </summary>
        public const uint DisplayconfigOutputTechnologyHdmi = 5;

        /// <summary>
        /// Output technology: DVI
        /// </summary>
        public const uint DisplayconfigOutputTechnologyDvi = 4;

        /// <summary>
        /// Output technology: DisplayPort
        /// </summary>
        public const uint DisplayconfigOutputTechnologyDisplayportExternal = 6;

        /// <summary>
        /// Output technology: internal
        /// </summary>
        public const uint DisplayconfigOutputTechnologyInternal = 0x80000000;

        /// <summary>
        /// HDR minimum SDR white level (nits)
        /// </summary>
        public const int HdrMinSdrWhiteLevel = 80;

        /// <summary>
        /// HDR maximum SDR white level (nits)
        /// </summary>
        public const int HdrMaxSdrWhiteLevel = 480;

        /// <summary>
        /// SDR white level conversion factor
        /// </summary>
        public const int SdrWhiteLevelFactor = 80;
    }
}
