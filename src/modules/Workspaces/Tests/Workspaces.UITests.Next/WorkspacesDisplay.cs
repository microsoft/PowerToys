// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.PowerToys.UITest.Next;

namespace Microsoft.Workspaces.UITests
{
    // Match DisplayUtils::GetDisplays rather than inventing monitor 1. A missing live monitor number
    // makes Workspaces minimize the app instead of restoring its requested placement.
    internal static class WorkspacesDisplay
    {
        private const uint DisplayDeviceActive = 0x1;
        private const uint DisplayDeviceMirroringDriver = 0x8;
        private const uint EddGetDeviceInterfaceName = 0x1;
        private const uint MonitorDefaultToPrimary = 0x1;
        private const int MdtEffectiveDpi = 0;

        // One connected display resolved to the product's identity/geometry contract. Rectangles are the
        // physical (DPI-aware) virtual-screen pixels read from a per-monitor-DPI-aware host; the logical
        // work-area helpers divide by the effective scale to reach the DPI-unaware coordinates Workspaces
        // persists and restores.
        internal sealed record TargetDisplay(
            string DeviceName,
            int Number,
            string Id,
            string InstanceId,
            uint Dpi,
            int MonitorLeft,
            int MonitorTop,
            int MonitorRight,
            int MonitorBottom,
            int WorkLeft,
            int WorkTop,
            int WorkRight,
            int WorkBottom,
            bool IsPrimary)
        {
            internal int MonitorWidth => MonitorRight - MonitorLeft;

            internal int MonitorHeight => MonitorBottom - MonitorTop;

            internal int LogicalMonitorLeft => Logical(MonitorLeft);

            internal int LogicalMonitorTop => Logical(MonitorTop);

            internal int LogicalMonitorRight => Logical(MonitorRight);

            internal int LogicalMonitorBottom => Logical(MonitorBottom);

            internal int LogicalWorkLeft => Logical(WorkLeft);

            internal int LogicalWorkTop => Logical(WorkTop);

            internal int LogicalWorkRight => Logical(WorkRight);

            internal int LogicalWorkBottom => Logical(WorkBottom);

            // Physical pixel -> DPI-unaware/logical coordinate (logical = physical * 96 / dpi), matching
            // WindowUtils::GetWindowRect (DPIAware::InverseConvert) so seeded positions round-trip.
            internal int Logical(int physical) => (int)Math.Round(physical * 96.0 / Dpi);
        }

        // A single row of EnumDisplayDevicesW output, reduced to the fields DisplayUtils reads.
        internal readonly record struct DisplayDeviceInfo(string DeviceName, string DeviceId, uint StateFlags);

        // Resolve the display containing the primary monitor to the product's identity/geometry. Throws an
        // actionable InvalidOperationException when the host has no usable display (a disconnected
        // RDP session), so the suite fails fast with guidance instead of a ~96s minimized-placement timeout.
        internal static TargetDisplay GetPrimaryTarget()
        {
            var primary = MonitorInfo.GetPrimary()
                ?? throw new InvalidOperationException(UnsupportedDesktopMessage("<none>"));
            var (number, id, instanceId) = ResolveNumberAndId(primary.DeviceName, EnumerateDevices(primary.DeviceName));
            var dpi = PrimaryDpi();
            return new TargetDisplay(
                primary.DeviceName,
                number,
                id,
                instanceId,
                dpi,
                primary.Left,
                primary.Top,
                primary.Right,
                primary.Bottom,
                primary.WorkLeft,
                primary.WorkTop,
                primary.WorkRight,
                primary.WorkBottom,
                primary.IsPrimary);
        }

        // Do not silently ignore an unresolvable display when choosing a genuinely absent monitor number.
        internal static IReadOnlyList<int> ConnectedMonitorNumbers()
        {
            var numbers = new List<int>();
            foreach (var monitor in MonitorInfo.GetAll())
            {
                numbers.Add(ResolveNumberAndId(monitor.DeviceName, EnumerateDevices(monitor.DeviceName)).Number);
            }

            return numbers;
        }

        // The smallest positive integer not present in the live topology. Provably absent even when real
        // displays are sparsely or highly numbered (e.g. only DISPLAY3 -> 1; DISPLAY1+DISPLAY2 -> 3).
        internal static int FirstUnusedNumber(IEnumerable<int> used)
        {
            var reserved = new HashSet<int>(used);
            var candidate = 1;
            while (reserved.Contains(candidate))
            {
                candidate++;
            }

            return candidate;
        }

        // A default application rectangle in the target's DPI-unaware coordinates, kept inside its work area
        // so the placement is not clamped by Windows on small monitors. On a typical work area whose logical
        // origin is (0,0) this preserves the historical (240, 220, 720, 460) baseline the editor field
        // assertions rely on; it only shrinks/offsets when the work area cannot contain it.
        internal static (int X, int Y, int Width, int Height) DefaultApplicationPosition(TargetDisplay target)
        {
            const int desiredX = 240;
            const int desiredY = 220;
            const int desiredWidth = 720;
            const int desiredHeight = 460;
            const int padding = 40;

            if (target.Dpi == 0)
            {
                throw new InvalidOperationException($"Display '{target.DeviceName}' has no usable DPI.");
            }

            var workLeft = target.LogicalWorkLeft;
            var workTop = target.LogicalWorkTop;
            var workWidth = target.LogicalWorkRight - workLeft;
            var workHeight = target.LogicalWorkBottom - workTop;
            if (workWidth <= 0 || workHeight <= 0)
            {
                throw new InvalidOperationException($"Display '{target.DeviceName}' has no usable work area or DPI.");
            }

            var width = Math.Clamp(desiredWidth, 1, Math.Max(1, workWidth - (2 * padding)));
            var height = Math.Clamp(desiredHeight, 1, Math.Max(1, workHeight - (2 * padding)));
            var x = workLeft + Math.Clamp(desiredX, Math.Min(padding, Math.Max(0, workWidth - width)), Math.Max(0, workWidth - width));
            var y = workTop + Math.Clamp(desiredY, Math.Min(padding, Math.Max(0, workHeight - height)), Math.Max(0, workHeight - height));
            return (x, y, width, height);
        }

        // Mirror of DisplayUtils::GetDisplays' per-monitor identity resolution: prefer the first active,
        // non-mirroring display device (its adapter name yields the number, its interface DeviceID the
        // id/instance-id); otherwise fall back to the GDI device name. Throws when no digits are present.
        internal static (int Number, string Id, string InstanceId) ResolveNumberAndId(string gdiDeviceName, IEnumerable<DisplayDeviceInfo> devices)
        {
            ArgumentNullException.ThrowIfNull(devices);
            foreach (var device in devices)
            {
                var active = (device.StateFlags & DisplayDeviceActive) == DisplayDeviceActive;
                var mirroring = (device.StateFlags & DisplayDeviceMirroringDriver) != 0;
                if (active && !mirroring)
                {
                    var (id, instanceId) = SplitDisplayDeviceId(device.DeviceId);
                    return (ParseNumber(TrimToAdapter(device.DeviceName), gdiDeviceName), id, instanceId);
                }
            }

            // No proper device: use the display name as the id and its digits as the number, as the product does.
            return (ParseNumber(gdiDeviceName, gdiDeviceName), gdiDeviceName, string.Empty);
        }

        // Mirror of DisplayUtils::SplitDisplayDeviceId.
        // \\?\DISPLAY#GSM1388#4&125707d6&0&UID8388688#{guid} -> (GSM1388, 4&125707d6&0&UID8388688)
        internal static (string Id, string InstanceId) SplitDisplayDeviceId(string deviceId)
        {
            ArgumentNullException.ThrowIfNull(deviceId);
            var nameStart = deviceId.IndexOf('#', StringComparison.Ordinal);
            var uidStart = nameStart < 0 ? -1 : deviceId.IndexOf('#', nameStart + 1);
            var uidEnd = uidStart < 0 ? -1 : deviceId.IndexOf('#', uidStart + 1);
            if (nameStart < 0 || uidStart < 0 || uidEnd < 0)
            {
                return (deviceId, string.Empty);
            }

            return (deviceId.Substring(nameStart + 1, uidStart - nameStart - 1), deviceId.Substring(uidStart + 1, uidEnd - uidStart - 1));
        }

        internal static string RemoveNonDigits(string input)
        {
            ArgumentNullException.ThrowIfNull(input);
            return new string(input.Where(char.IsDigit).ToArray());
        }

        private static string TrimToAdapter(string deviceName)
        {
            // \\.\DISPLAY1\Monitor0 -> \\.\DISPLAY1 (substring before the last backslash), matching the product.
            var lastBackslash = deviceName.LastIndexOf('\\');
            return lastBackslash > 0 ? deviceName[..lastBackslash] : deviceName;
        }

        private static int ParseNumber(string source, string gdiDeviceName)
        {
            var digits = RemoveNonDigits(source);
            if (digits.Length > 0 && int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            {
                return number;
            }

            throw new InvalidOperationException(UnsupportedDesktopMessage(gdiDeviceName));
        }

        private static string UnsupportedDesktopMessage(string deviceName) =>
            $"Workspaces UI tests need a connected display to validate window placement, but '{deviceName}' has no GDI monitor number. " +
            "A disconnected RDP session can expose the numberless 'WinDisc' pseudo-display, causing Workspaces to use its minimized-window fallback. " +
            "Connect and unlock the desktop (physical or RDP), keep it connected during the run, and use non-elevated Test Explorer.";

        private static IEnumerable<DisplayDeviceInfo> EnumerateDevices(string gdiDeviceName)
        {
            var device = new DISPLAY_DEVICE { Cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
            uint index = 0;
            while (EnumDisplayDevicesW(gdiDeviceName, index, ref device, EddGetDeviceInterfaceName))
            {
                yield return new DisplayDeviceInfo(device.DeviceName, device.DeviceID, device.StateFlags);
                index++;
                device = new DISPLAY_DEVICE { Cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
            }
        }

        private static uint PrimaryDpi()
        {
            var monitor = MonitorFromPoint(default, MonitorDefaultToPrimary);
            if (monitor == IntPtr.Zero || GetDpiForMonitor(monitor, MdtEffectiveDpi, out var dpiX, out _) != 0 || dpiX == 0)
            {
                throw new InvalidOperationException("Could not read the effective DPI of the primary monitor.");
            }

            return dpiX;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumDisplayDevicesW(string? lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr monitor, int dpiType, out uint dpiX, out uint dpiY);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DISPLAY_DEVICE
        {
            public int Cb;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceString;

            public uint StateFlags;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceID;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceKey;
        }
    }
}
