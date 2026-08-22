// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerDisplay.Common.Drivers
{
    /// <summary>
    /// Windows API constant definitions
    /// </summary>
    public static class NativeConstants
    {
        /// <summary>
        /// VCP code: Brightness (0x10)
        /// Standard VESA MCCS brightness control.
        /// This is the ONLY brightness code used by PowerDisplay.
        /// </summary>
        public const byte VcpCodeBrightness = 0x10;

        /// <summary>
        /// VCP code: Contrast (0x12)
        /// Standard VESA MCCS contrast control.
        /// </summary>
        public const byte VcpCodeContrast = 0x12;

        /// <summary>
        /// VCP code: Audio Speaker Volume (0x62)
        /// Standard VESA MCCS volume control for monitors with built-in speakers.
        /// </summary>
        public const byte VcpCodeVolume = 0x62;

        /// <summary>
        /// VCP code: Select Color Preset (0x14)
        /// Standard VESA MCCS color temperature preset selection.
        /// Supports discrete values like: 0x01=sRGB, 0x04=5000K, 0x05=6500K, 0x08=9300K.
        /// This is the standard method for color temperature control.
        /// </summary>
        public const byte VcpCodeSelectColorPreset = 0x14;

        /// <summary>
        /// VCP code: Input Source (0x60)
        /// Standard VESA MCCS input source selection.
        /// Supports values like: 0x0F=DisplayPort-1, 0x10=DisplayPort-2, 0x11=HDMI-1, 0x12=HDMI-2, 0x1B=USB-C.
        /// Note: Actual supported values depend on monitor capabilities.
        /// </summary>
        public const byte VcpCodeInputSource = 0x60;

        /// <summary>
        /// VCP code: Power Mode (0xD6)
        /// Controls monitor power state via DPMS.
        /// Values: 0x01=On, 0x02=Standby, 0x03=Suspend, 0x04=Off(DPM), 0x05=Off(Hard).
        /// Note: Switching to any non-On state will turn off the display.
        /// </summary>
        public const byte VcpCodePowerMode = 0xD6;

        /// <summary>
        /// Query display config: only active paths
        /// </summary>
        public const uint QdcOnlyActivePaths = 0x00000002;

        /// <summary>
        /// Get source name (GDI device name like "\\.\DISPLAY1")
        /// </summary>
        public const uint DisplayconfigDeviceInfoGetSourceName = 1;

        /// <summary>
        /// Get target name (monitor friendly name and hardware ID)
        /// </summary>
        public const uint DisplayconfigDeviceInfoGetTargetName = 2;

        /// <summary>
        /// Retrieve the current settings for the display device.
        /// </summary>
        public const int EnumCurrentSettings = -1;

        /// <summary>
        /// The display is in the natural orientation of the device.
        /// </summary>
        public const int DmdoDefault = 0;

        /// <summary>
        /// The display is rotated 180 degrees (measured clockwise) from its natural orientation.
        /// </summary>
        public const int Dmdo180 = 2;

        // ==================== DEVMODE field flags ====================

        /// <summary>
        /// DmDisplayOrientation field is valid.
        /// </summary>
        public const int DmDisplayOrientation = 0x00000080;

        /// <summary>
        /// DmPelsWidth field is valid.
        /// </summary>
        public const int DmPelsWidth = 0x00080000;

        /// <summary>
        /// DmPelsHeight field is valid.
        /// </summary>
        public const int DmPelsHeight = 0x00100000;

        // ==================== ChangeDisplaySettings flags ====================

        /// <summary>
        /// Test the graphics mode but don't actually set it.
        /// </summary>
        public const uint CdsTest = 0x00000002;

        // ==================== ChangeDisplaySettings result codes ====================

        /// <summary>
        /// The settings change was successful.
        /// </summary>
        public const int DispChangeSuccessful = 0;

        /// <summary>
        /// The computer must be restarted for the graphics mode to work.
        /// </summary>
        public const int DispChangeRestart = 1;

        /// <summary>
        /// The display driver failed the specified graphics mode.
        /// </summary>
        public const int DispChangeFailed = -1;

        /// <summary>
        /// The graphics mode is not supported.
        /// </summary>
        public const int DispChangeBadmode = -2;

        /// <summary>
        /// Unable to write settings to the registry.
        /// </summary>
        public const int DispChangeNotupdated = -3;

        /// <summary>
        /// An invalid set of flags was passed in.
        /// </summary>
        public const int DispChangeBadflags = -4;

        /// <summary>
        /// An invalid parameter was passed in.
        /// </summary>
        public const int DispChangeBadparam = -5;
    }
}
