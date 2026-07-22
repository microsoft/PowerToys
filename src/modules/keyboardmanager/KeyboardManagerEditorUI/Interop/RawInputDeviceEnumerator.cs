// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ManagedCommon;

namespace KeyboardManagerEditorUI.Interop
{
    /// <summary>
    /// Enumerates connected keyboards using Raw Input (no window required). Mirrors the engine's
    /// identity model: device paths are normalized to the stable prefix (up to the 2nd '#') and
    /// de-duplicated, so the list matches what the engine writes into the device→profile map.
    /// </summary>
    internal static class RawInputDeviceEnumerator
    {
        private const uint RidiDeviceName = 0x20000007;
        private const uint RimTypeKeyboard = 1;
        private const uint FileShareRead = 0x1;
        private const uint FileShareWrite = 0x2;
        private const uint OpenExisting = 3;

        [StructLayout(LayoutKind.Sequential)]
        private struct RawInputDeviceList
        {
            public IntPtr HDevice;
            public uint DwType;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetRawInputDeviceList([In, Out] RawInputDeviceList[]? pRawInputDeviceList, ref uint puiNumDevices, uint cbSize);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern uint GetRawInputDeviceInfoW(IntPtr hDevice, uint uiCommand, IntPtr pData, ref uint pcbSize);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateFileW(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("hid.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool HidD_GetProductString(IntPtr hidDeviceObject, IntPtr buffer, uint bufferLength);

        /// <summary>
        /// Normalizes a RIDI_DEVICENAME to the stable prefix (everything before the 2nd '#'),
        /// dropping the instance id that some virtual keyboards churn. Must match the engine's
        /// NormalizeDevicePath so the UI and the engine agree on device identity.
        /// </summary>
        public static string NormalizeDevicePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            int first = path.IndexOf('#', StringComparison.Ordinal);
            if (first < 0)
            {
                return path;
            }

            int second = path.IndexOf('#', first + 1);
            return second < 0 ? path : path.Substring(0, second);
        }

        public static List<DetectedKeyboard> EnumerateKeyboards()
        {
            var result = new List<DetectedKeyboard>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                uint count = 0;
                uint structSize = (uint)Marshal.SizeOf<RawInputDeviceList>();
                if (GetRawInputDeviceList(null, ref count, structSize) != 0 || count == 0)
                {
                    return result;
                }

                var list = new RawInputDeviceList[count];
                if (GetRawInputDeviceList(list, ref count, structSize) == unchecked((uint)-1))
                {
                    return result;
                }

                for (int i = 0; i < count; i++)
                {
                    if (list[i].DwType != RimTypeKeyboard)
                    {
                        continue;
                    }

                    DetectedKeyboard? keyboard = DescribeDevice(list[i].HDevice);
                    if (keyboard is null || !seen.Add(keyboard.DevicePath))
                    {
                        continue; // unreadable, or already listed (one entry per physical device)
                    }

                    result.Add(keyboard);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to enumerate keyboards: " + ex.Message);
            }

            return result;
        }

        /// <summary>
        /// Describes a raw-input device handle (as delivered with WM_INPUT) as a keyboard entry, or
        /// null if its name can't be read. Used to identify the keyboard a live keystroke came from.
        /// </summary>
        public static DetectedKeyboard? DescribeDevice(IntPtr hDevice)
        {
            string fullPath = GetDeviceName(hDevice);
            if (string.IsNullOrEmpty(fullPath))
            {
                return null;
            }

            return new DetectedKeyboard
            {
                DevicePath = NormalizeDevicePath(fullPath),
                DisplayName = BuildDisplayName(fullPath),
            };
        }

        private static string GetDeviceName(IntPtr hDevice)
        {
            uint size = 0;
            if (GetRawInputDeviceInfoW(hDevice, RidiDeviceName, IntPtr.Zero, ref size) != 0 || size == 0)
            {
                return string.Empty;
            }

            IntPtr buffer = Marshal.AllocHGlobal((int)size * sizeof(char));
            try
            {
                if (GetRawInputDeviceInfoW(hDevice, RidiDeviceName, buffer, ref size) == unchecked((uint)-1))
                {
                    return string.Empty;
                }

                return Marshal.PtrToStringUni(buffer) ?? string.Empty;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private static string BuildDisplayName(string fullPath)
        {
            string product = GetProductString(fullPath);
            if (!string.IsNullOrEmpty(product))
            {
                return product;
            }

            string vid = ExtractToken(fullPath, "VID");
            string pid = ExtractToken(fullPath, "PID");
            if (!string.IsNullOrEmpty(vid))
            {
                return $"Keyboard (VID {vid}/PID {pid})";
            }

            // No friendly identity (e.g. virtualized provider): show the stable device token.
            return ShortenPath(NormalizeDevicePath(fullPath));
        }

        private static string GetProductString(string devicePath)
        {
            IntPtr handle = CreateFileW(devicePath, 0, FileShareRead | FileShareWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
            if (handle == IntPtr.Zero || handle == new IntPtr(-1))
            {
                return string.Empty;
            }

            const int bytes = 256 * sizeof(char);
            IntPtr buffer = Marshal.AllocHGlobal(bytes);
            try
            {
                if (!HidD_GetProductString(handle, buffer, bytes))
                {
                    return string.Empty;
                }

                return (Marshal.PtrToStringUni(buffer) ?? string.Empty).Trim();
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
                CloseHandle(handle);
            }
        }

        private static string ExtractToken(string path, string key)
        {
            string upper = path.ToUpperInvariant();
            foreach (char sep in new[] { '_', '&' })
            {
                int pos = upper.IndexOf(key + sep, StringComparison.Ordinal);
                if (pos < 0)
                {
                    continue;
                }

                pos += key.Length + 1;
                int start = pos;
                while (pos < upper.Length && Uri.IsHexDigit(upper[pos]))
                {
                    pos++;
                }

                string hex = upper.Substring(start, pos - start);
                if (hex.Length == 0)
                {
                    continue;
                }

                // Bluetooth encodes VID as "0002<realVid>"; keep the last 4 hex digits.
                return hex.Length == 8 ? hex.Substring(4) : hex;
            }

            return string.Empty;
        }

        private static string ShortenPath(string path)
        {
            int hash = path.IndexOf('#', StringComparison.Ordinal);
            string tail = hash >= 0 && hash + 1 < path.Length ? path[(hash + 1)..] : path;
            return tail.Length > 40
                ? string.Concat("Keyboard (", tail.AsSpan(0, 40), "…)")
                : "Keyboard (" + tail + ")";
        }
    }
}
