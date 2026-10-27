// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.Win32.SafeHandles;

namespace Microsoft.GrabAndMove.UITests;

internal static class HidInput
{
    private static readonly Lazy<SafeFileHandle> Device = new(OpenControlDevice);
    private static byte modifiers;
    private static byte buttons;
    private static int mouseX;
    private static int mouseY;
    private static bool hasPosition;

    internal static string DevicePath { get; private set; } = string.Empty;

    internal static bool IsConnected => Device.IsValueCreated;

    internal static void SetModifier(Key key, bool pressed)
    {
        byte mask = key switch
        {
            Key.Ctrl or Key.LCtrl => 1,
            Key.Shift or Key.LShift => 2,
            Key.Alt => 4,
            Key.LWin => 8,
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, "The prototype exposes modifier HID usages only."),
        };
        modifiers = pressed ? (byte)(modifiers | mask) : (byte)(modifiers & ~mask);
        Submit([2, modifiers, 0, 0, 0, 0, 0, 0, 0]);
    }

    internal static void SetButton(byte button, bool pressed)
    {
        buttons = pressed ? (byte)(buttons | button) : (byte)(buttons & ~button);
        if (!hasPosition)
        {
            (mouseX, mouseY) = MouseHelper.GetMousePosition();
        }

        MoveTo(mouseX, mouseY);
    }

    internal static void MoveTo(int x, int y)
    {
        var screen = System.Windows.Forms.SystemInformation.PrimaryMonitorSize;
        var rawX = Normalize(x, screen.Width);
        var rawY = Normalize(y, screen.Height);
        Submit([3, buttons, (byte)(rawX & 255), (byte)(rawX >> 8), (byte)(rawY & 255), (byte)(rawY >> 8), 0]);
        mouseX = x;
        mouseY = y;
        hasPosition = true;
    }

    private static ushort Normalize(int pixel, int extent) =>
        checked((ushort)Math.Clamp((int)Math.Round((pixel + 0.5) * 32767 / extent), 0, 32767));

    private static void Submit(byte[] input)
    {
        var packet = new byte[16];
        packet[0] = 1;
        packet[1] = 0x10;
        input.CopyTo(packet, 2);
        var timer = Stopwatch.StartNew();
        while (!HidD_SetFeature(Device.Value, packet, packet.Length))
        {
            var error = Marshal.GetLastWin32Error();
            if (error != 21 || timer.ElapsedMilliseconds > 5_000)
            {
                throw new Win32Exception(error, $"HID feature transport failed for report {input[0]} on '{DevicePath}'.");
            }

            Thread.Sleep(10);
        }
    }

    private static SafeFileHandle OpenControlDevice()
    {
        HidD_GetHidGuid(out var guid);
        var devices = SetupDiGetClassDevsW(ref guid, null, IntPtr.Zero, 0x12);
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
                    if (error == 259)
                    {
                        break;
                    }

                    throw new Win32Exception(error);
                }

                _ = SetupDiGetDeviceInterfaceDetailW(devices, ref data, IntPtr.Zero, 0, out var required, IntPtr.Zero);
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
                    var matches = HidD_GetAttributes(handle, ref attributes) && attributes.Vendor == 0xDEED && attributes.Product == 0xFEED;
                    if (matches && HidD_GetPreparsedData(handle, out var preparsed))
                    {
                        try
                        {
                            matches = HidP_GetCaps(preparsed, out var caps) == 0x110000 &&
                                caps.UsagePage == 0xFF00 && caps.Usage == 1 && caps.FeatureLength == 16;
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

        throw new InvalidOperationException("No UMDF prototype control collection was found. There is deliberately no SendInput fallback.");
    }

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
