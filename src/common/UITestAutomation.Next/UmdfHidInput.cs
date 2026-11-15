// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Microsoft.PowerToys.UITest.Next;

/// <summary>
/// Opts the keyboard and mouse helpers into the installed PowerToys virtual HID test device.
/// The scope is process-wide: do not run input tests in parallel. Activation fails if the driver
/// is unavailable; there is no injected-input fallback or automatic driver installation.
/// </summary>
public sealed class UmdfHidInput : IDisposable
{
    // Keep this protocol in sync with .pipelines\umdf-hid-smoke\vhidmini-input.patch.
    private const byte ControlReportId = 1;
    private const byte SubmitInputCommand = 0x10;
    private const byte KeyboardReportId = 2;
    private const byte MouseReportId = 3;
    private const int ControlReportLength = 16;
    private const int KeyboardReportLength = 9;
    private const int KeyboardKeyCapacity = 6;
    private const ushort DeviceVendorId = 0xDEED;
    private const ushort DeviceProductId = 0xFEED;
    private const ushort ControlUsagePage = 0xFF00;
    private const ushort ControlUsage = 1;
    private const int ErrorNotReady = 21;
    private const int ErrorNoMoreItems = 259;
    private const int ErrorInsufficientBuffer = 122;
    private const uint SetupDiPresent = 0x2;
    private const uint SetupDiDeviceInterface = 0x10;
    private const int HidStatusSuccess = 0x110000;
    private static readonly object ModeLock = new();
    private static UmdfHidInput? active;
    private readonly SafeFileHandle device;
    private readonly Dictionary<Key, byte> heldKeys = [];
    private byte modifiers;
    private byte buttons;
    private int mouseX;
    private int mouseY;
    private bool hasMousePosition;
    private bool disposed;

    private UmdfHidInput()
    {
        device = OpenControlDevice();
    }

    /// <summary>Device interface selected for this input scope.</summary>
    public string DevicePath { get; private set; } = string.Empty;

    internal static UmdfHidInput? Active => Volatile.Read(ref active);

    /// <summary>Enable real HID input until the returned scope is disposed.</summary>
    public static UmdfHidInput Activate()
    {
        lock (ModeLock)
        {
            if (active is not null)
            {
                throw new InvalidOperationException("A process-wide UMDF HID input scope is already active.");
            }

            var input = new UmdfHidInput();
            Volatile.Write(ref active, input);
            return input;
        }
    }

    /// <summary>Release held HID keys/buttons, close the device, and restore the default helpers.</summary>
    public void Dispose()
    {
        lock (ModeLock)
        {
            if (disposed)
            {
                return;
            }

            try
            {
                Submit(CreateKeyboardReport(0, []));
            }
            finally
            {
                try
                {
                    if (buttons != 0)
                    {
                        buttons = 0;
                        SubmitMouse();
                    }
                }
                finally
                {
                    disposed = true;
                    Volatile.Write(ref active, null);
                    device.Dispose();
                }
            }
        }

        GC.SuppressFinalize(this);
    }

    internal void SetKey(Key key, bool pressed)
    {
        lock (ModeLock)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            var mask = ModifierMask(key);
            if (mask != 0)
            {
                modifiers = pressed ? (byte)(modifiers | mask) : (byte)(modifiers & ~mask);
            }
            else if (pressed)
            {
                var usage = KeyToUsage(key, MapVirtualKeyW((uint)key, 4));
                var usages = heldKeys.Where(pair => pair.Key != key).Select(pair => pair.Value).Append(usage);
                _ = CreateKeyboardReport(modifiers, usages);
                heldKeys[key] = usage;
            }
            else
            {
                heldKeys.Remove(key);
            }

            Submit(CreateKeyboardReport(modifiers, heldKeys.Values));
        }
    }

    internal void SendKeys(Key[] keys)
    {
        try
        {
            foreach (var key in keys)
            {
                SetKey(key, true);
            }

            Thread.Sleep(20);
        }
        finally
        {
            foreach (var key in keys.Reverse())
            {
                SetKey(key, false);
            }
        }
    }

    internal void MoveTo(int x, int y)
    {
        lock (ModeLock)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            var screen = System.Windows.Forms.SystemInformation.PrimaryMonitorSize;
            _ = Normalize(x, screen.Width);
            _ = Normalize(y, screen.Height);
            mouseX = x;
            mouseY = y;
            hasMousePosition = true;
            SubmitMouse();
        }
    }

    internal void SendMouseInput(uint flags, int data, int dx, int dy)
    {
        lock (ModeLock)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (!hasMousePosition)
            {
                (mouseX, mouseY) = MouseHelper.GetMousePosition();
                var screen = System.Windows.Forms.SystemInformation.PrimaryMonitorSize;
                _ = Normalize(mouseX, screen.Width);
                _ = Normalize(mouseY, screen.Height);
                hasMousePosition = true;
            }

            switch (flags)
            {
                case 0x01:
                    MoveTo(mouseX + dx, mouseY + dy);
                    return;
                case 0x02: buttons |= 1; break;
                case 0x04: buttons &= unchecked((byte)~1); break;
                case 0x08: buttons |= 2; break;
                case 0x10: buttons &= unchecked((byte)~2); break;
                case 0x20: buttons |= 4; break;
                case 0x40: buttons &= unchecked((byte)~4); break;
                case 0x800:
                    if (data % 120 != 0)
                    {
                        throw new ArgumentOutOfRangeException(nameof(data), "The HID wheel supports whole 120-unit detents only.");
                    }

                    var ticks = data / 120;
                    while (ticks != 0)
                    {
                        var step = Math.Clamp(ticks, -127, 127);
                        SubmitMouse((sbyte)step);
                        ticks -= step;
                    }

                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(flags), flags, "Unsupported HID mouse operation.");
            }

            SubmitMouse();
        }
    }

    internal static byte ModifierMask(Key key) => key switch
    {
        Key.Ctrl or Key.LCtrl => 1,
        Key.Shift or Key.LShift => 2,
        Key.Alt => 4,
        Key.LWin => 8,
        Key.RCtrl => 16,
        _ => 0,
    };

    internal static byte KeyToUsage(Key key, uint scanCode) =>
        ScanCodeToUsage(KeyboardHelper.IsExtended(key) ? scanCode | 0xe000 : scanCode);

    internal static byte[] CreateKeyboardReport(byte modifier, IEnumerable<byte> usages)
    {
        ArgumentNullException.ThrowIfNull(usages);
        var keys = usages.Distinct().Order().ToArray();
        if (keys.Length > KeyboardKeyCapacity)
        {
            throw new InvalidOperationException("The virtual HID keyboard supports at most six simultaneous non-modifier keys.");
        }

        var report = new byte[KeyboardReportLength];
        report[0] = KeyboardReportId;
        report[1] = modifier;
        keys.CopyTo(report, 3);
        return report;
    }

    internal static ushort Normalize(int pixel, int extent)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(extent);
        if (pixel < 0 || pixel >= extent)
        {
            throw new ArgumentOutOfRangeException(nameof(pixel), pixel, "The virtual HID mouse supports primary-monitor coordinates only.");
        }

        return checked((ushort)Math.Round((pixel + 0.5) * 32767 / extent));
    }

    internal static byte ScanCodeToUsage(uint scanCode) => scanCode switch
    {
        0x1e => 0x04, 0x30 => 0x05, 0x2e => 0x06, 0x20 => 0x07, 0x12 => 0x08,
        0x21 => 0x09, 0x22 => 0x0a, 0x23 => 0x0b, 0x17 => 0x0c, 0x24 => 0x0d,
        0x25 => 0x0e, 0x26 => 0x0f, 0x32 => 0x10, 0x31 => 0x11, 0x18 => 0x12,
        0x19 => 0x13, 0x10 => 0x14, 0x13 => 0x15, 0x1f => 0x16, 0x14 => 0x17,
        0x16 => 0x18, 0x2f => 0x19, 0x11 => 0x1a, 0x2d => 0x1b, 0x15 => 0x1c,
        0x2c => 0x1d,
        >= 0x02 and <= 0x0a => (byte)(0x1e + scanCode - 0x02),
        0x0b => 0x27, 0x1c => 0x28, 0x01 => 0x29, 0x0e => 0x2a, 0x0f => 0x2b,
        0x39 => 0x2c, 0x0c => 0x2d, 0x0d => 0x2e, 0x1a => 0x2f, 0x1b => 0x30,
        0x2b => 0x31, 0x27 => 0x33, 0x28 => 0x34, 0x29 => 0x35, 0x33 => 0x36,
        0x34 => 0x37, 0x35 => 0x38, 0x56 => 0x64,
        >= 0x3b and <= 0x44 => (byte)(0x3a + scanCode - 0x3b),
        0x57 => 0x44, 0x58 => 0x45,
        0xe052 => 0x49, 0xe047 => 0x4a, 0xe049 => 0x4b, 0xe053 => 0x4c,
        0xe04f => 0x4d, 0xe051 => 0x4e, 0xe04d => 0x4f, 0xe04b => 0x50,
        0xe050 => 0x51, 0xe048 => 0x52,
        _ => throw new NotSupportedException($"No keyboard HID usage is defined for scan code 0x{scanCode:X}."),
    };

    private void SubmitMouse(sbyte wheel = 0)
    {
        var screen = System.Windows.Forms.SystemInformation.PrimaryMonitorSize;
        var x = Normalize(mouseX, screen.Width);
        var y = Normalize(mouseY, screen.Height);
        Submit([MouseReportId, buttons, (byte)(x & 255), (byte)(x >> 8), (byte)(y & 255), (byte)(y >> 8), unchecked((byte)wheel)]);
    }

    private void Submit(byte[] input)
    {
        var packet = new byte[ControlReportLength];
        packet[0] = ControlReportId;
        packet[1] = SubmitInputCommand;
        input.CopyTo(packet, 2);
        var timer = Stopwatch.StartNew();
        while (!HidD_SetFeature(device, packet, packet.Length))
        {
            var error = Marshal.GetLastWin32Error();
            if (error != ErrorNotReady || timer.ElapsedMilliseconds > 5_000)
            {
                throw new Win32Exception(error, $"HID feature transport failed for report {input[0]} on '{DevicePath}'.");
            }

            Thread.Sleep(10);
        }
    }

    private SafeFileHandle OpenControlDevice()
    {
        HidD_GetHidGuid(out var guid);
        var devices = SetupDiGetClassDevsW(ref guid, null, IntPtr.Zero, SetupDiPresent | SetupDiDeviceInterface);
        if (devices == new IntPtr(-1))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        try
        {
            for (uint index = 0; ; index++)
            {
                var data = new DeviceInterfaceData { Size = Marshal.SizeOf<DeviceInterfaceData>() };
                if (!SetupDiEnumDeviceInterfaces(devices, IntPtr.Zero, ref guid, index, ref data))
                {
                    var error = Marshal.GetLastWin32Error();
                    if (error == ErrorNoMoreItems)
                    {
                        break;
                    }

                    throw new Win32Exception(error);
                }

                _ = SetupDiGetDeviceInterfaceDetailW(devices, ref data, IntPtr.Zero, 0, out var required, IntPtr.Zero);
                var sizeError = Marshal.GetLastWin32Error();
                if (sizeError != ErrorInsufficientBuffer || required < 8)
                {
                    throw new Win32Exception(sizeError, "Could not determine the HID interface detail buffer size.");
                }

                var detail = Marshal.AllocHGlobal(checked((int)required));
                try
                {
                    Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                    if (!SetupDiGetDeviceInterfaceDetailW(devices, ref data, detail, required, out _, IntPtr.Zero))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }

                    var path = Marshal.PtrToStringUni(detail + 4)
                        ?? throw new InvalidOperationException("The HID interface path was empty.");
                    var handle = CreateFileW(path, 0xC0000000, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
                    if (handle.IsInvalid)
                    {
                        handle.Dispose();
                        continue;
                    }

                    var attributes = new HidAttributes { Size = Marshal.SizeOf<HidAttributes>() };
                    var matches = HidD_GetAttributes(handle, ref attributes) && attributes.Vendor == DeviceVendorId && attributes.Product == DeviceProductId;
                    if (matches && HidD_GetPreparsedData(handle, out var preparsed))
                    {
                        try
                        {
                            matches = HidP_GetCaps(preparsed, out var caps) == HidStatusSuccess &&
                                caps.UsagePage == ControlUsagePage && caps.Usage == ControlUsage && caps.FeatureLength == ControlReportLength;
                        }
                        finally
                        {
                            HidD_FreePreparsedData(preparsed);
                        }
                    }
                    else
                    {
                        matches = false;
                    }

                    if (matches)
                    {
                        DevicePath = path;
                        return handle;
                    }

                    handle.Dispose();
                }
                finally
                {
                    Marshal.FreeHGlobal(detail);
                }
            }
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(devices);
        }

        throw new InvalidOperationException("No PowerToys virtual HID test device was found. Provision the test driver before activating UMDF input; there is no SendInput fallback.");
    }

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKeyW(uint code, uint mapType);

    [StructLayout(LayoutKind.Sequential)]
    private struct DeviceInterfaceData
    {
        internal int Size;
        internal Guid Guid;
        internal int Flags;
        internal IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HidAttributes
    {
        internal int Size;
        internal ushort Vendor;
        internal ushort Product;
        internal ushort Version;
    }

    [StructLayout(LayoutKind.Sequential, Size = 64)]
    private struct HidCaps
    {
        internal ushort Usage;
        internal ushort UsagePage;
        internal ushort InputLength;
        internal ushort OutputLength;
        internal ushort FeatureLength;
    }

    [DllImport("hid.dll")]
    private static extern void HidD_GetHidGuid(out Guid guid);

    [DllImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool HidD_GetAttributes(SafeFileHandle device, ref HidAttributes attributes);

    [DllImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool HidD_GetPreparsedData(SafeFileHandle device, out IntPtr data);

    [DllImport("hid.dll")]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool HidD_FreePreparsedData(IntPtr data);

    [DllImport("hid.dll")]
    private static extern int HidP_GetCaps(IntPtr data, out HidCaps caps);

    [DllImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool HidD_SetFeature(SafeFileHandle device, byte[] report, int length);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevsW(ref Guid guid, string? enumerator, IntPtr parent, uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiEnumDeviceInterfaces(IntPtr devices, IntPtr device, ref Guid guid, uint index, ref DeviceInterfaceData data);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiGetDeviceInterfaceDetailW(IntPtr devices, ref DeviceInterfaceData data, IntPtr detail, uint size, out uint required, IntPtr device);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr devices);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
}
