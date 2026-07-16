// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.UITest.Next;

/// <summary>
/// Display-mode helpers used only by the pipeline path of <see cref="UITestBase"/>: pin the primary
/// display to a known resolution so coordinate-sensitive tests are deterministic in CI, and dump the
/// monitor topology for post-mortem diagnostics. Native because winappcli exposes no display API.
/// </summary>
public static class DisplayHelper
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int EnumDisplaySettings(string? lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int ChangeDisplaySettings(ref DEVMODE lpDevMode, int dwflags);

    private const int ENUM_CURRENT_SETTINGS = -1;
    private const int CDS_TEST = 0x00000002;
    private const int CDS_UPDATEREGISTRY = 0x00000001;
    private const int DISP_CHANGE_SUCCESSFUL = 0;
    private const int DM_PELSWIDTH = 0x00080000;
    private const int DM_PELSHEIGHT = 0x00100000;

    /// <summary>
    /// Pin the primary display to <paramref name="width"/> x <paramref name="height"/>. No-op when
    /// already at that resolution. Best-effort — swallows failures because a CI agent may disallow
    /// display-mode changes.
    /// </summary>
    /// <remarks>
    /// Unlike the legacy harness (which left <c>dmFields</c> unset), this reads the current mode via
    /// <c>EnumDisplaySettings(ENUM_CURRENT_SETTINGS)</c> and sets
    /// <c>DM_PELSWIDTH | DM_PELSHEIGHT</c> — the documented, reliable way to request a resolution
    /// change.
    /// </remarks>
    public static void NormalizeResolution(int width, int height)
    {
        try
        {
            var primary = Screen.PrimaryScreen;
            if (primary is not null && primary.Bounds.Width == width && primary.Bounds.Height == height)
            {
                return;
            }

            var devMode = default(DEVMODE);
            devMode.DmDeviceName = new string('\0', 32);
            devMode.DmFormName = new string('\0', 32);
            devMode.DmSize = (short)Marshal.SizeOf<DEVMODE>();

            if (EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref devMode) == 0)
            {
                return;
            }

            devMode.DmPelsWidth = width;
            devMode.DmPelsHeight = height;
            devMode.DmFields = DM_PELSWIDTH | DM_PELSHEIGHT;

            if (ChangeDisplaySettings(ref devMode, CDS_TEST) == DISP_CHANGE_SUCCESSFUL)
            {
                ChangeDisplaySettings(ref devMode, CDS_UPDATEREGISTRY);
            }
        }
        catch
        {
            // Resolution normalization is a CI nicety, not a hard requirement.
        }
    }

    /// <summary>Write the connected-monitor topology to the test log (and console) for diagnostics.</summary>
    public static void LogMonitors(TestContext? testContext = null)
    {
        try
        {
            foreach (var m in MonitorInfo.GetAll())
            {
                var line = $"Monitor '{m.DeviceName}': {m.Width}x{m.Height} at ({m.Left},{m.Top}) primary={m.IsPrimary}";
                testContext?.WriteLine(line);
                Console.WriteLine(line);
            }
        }
        catch
        {
            // Diagnostics only — never let logging fail a test.
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DmDeviceName;
        public short DmSpecVersion;
        public short DmDriverVersion;
        public short DmSize;
        public short DmDriverExtra;
        public int DmFields;
        public int DmPositionX;
        public int DmPositionY;
        public int DmDisplayOrientation;
        public int DmDisplayFixedOutput;
        public short DmColor;
        public short DmDuplex;
        public short DmYResolution;
        public short DmTTOption;
        public short DmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DmFormName;
        public short DmLogPixels;
        public int DmBitsPerPel;
        public int DmPelsWidth;
        public int DmPelsHeight;
        public int DmDisplayFlags;
        public int DmDisplayFrequency;
        public int DmICMMethod;
        public int DmICMIntent;
        public int DmMediaType;
        public int DmDitherType;
        public int DmReserved1;
        public int DmReserved2;
        public int DmPanningWidth;
        public int DmPanningHeight;
    }
}
