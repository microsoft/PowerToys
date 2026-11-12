// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.PowerToys.Common.UI.Controls
{
    // Reads the icon out of an executable. The shell does not answer from inside the window, so it is taken from the file itself.
    public static class IconReader
    {
        // The size of the block that Windows keeps for an icon.
        private const int IconInfoSize = 32;

        // The place of the mask of an icon in that block.
        private const int MaskOffset = 16;

        // The place of the picture of an icon in that block.
        private const int ColorOffset = 24;

        // The header that describes a picture to the drawing part of Windows.
        [StructLayout(LayoutKind.Sequential)]
        private struct BitmapHeader
        {
            public uint Size;
            public int Width;
            public int Height;
            public ushort Planes;
            public ushort BitsPerPixel;
            public uint Compression;
            public uint SizeImage;
            public int XPixelsPerMeter;
            public int YPixelsPerMeter;
            public uint ColorsUsed;
            public uint ColorsImportant;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "ExtractIconExW")]
        private static extern int ExtractIconEx(string fileName, int iconIndex, out IntPtr largeIcon, out IntPtr smallIcon, uint icons);

        [DllImport("user32.dll", EntryPoint = "GetIconInfo")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetIconInfo(IntPtr icon, IntPtr info);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr window);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr window, IntPtr deviceContext);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr icon);

        [DllImport("gdi32.dll")]
        private static extern int GetDIBits(
            IntPtr deviceContext,
            IntPtr bitmap,
            uint start,
            uint lines,
            byte[] bits,
            ref BitmapHeader header,
            uint usage);

        // The picture comes back one pixel after the other in blue, green, red and alpha. Nothing comes back when there is no icon.
        public static byte[]? ReadIcon(string path, int size)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            if (ExtractIconEx(path, 0, out var large, out var small, 1) == 0)
            {
                return null;
            }

            try
            {
                var icon = large != IntPtr.Zero ? large : small;
                if (icon == IntPtr.Zero)
                {
                    return null;
                }

                var mask = IntPtr.Zero;
                var color = IntPtr.Zero;

                var info = Marshal.AllocHGlobal(IconInfoSize);
                try
                {
                    for (var i = 0; i < IconInfoSize; i++)
                    {
                        Marshal.WriteByte(info, i, 0);
                    }

                    if (!GetIconInfo(icon, info))
                    {
                        return null;
                    }

                    mask = Handle(Marshal.ReadInt64(info, MaskOffset));
                    color = Handle(Marshal.ReadInt64(info, ColorOffset));
                }
                finally
                {
                    Marshal.FreeHGlobal(info);
                }

                var bits = ReadBitmap(color, size);
                if (bits == null)
                {
                    return null;
                }

                if (!HasAlpha(bits))
                {
                    // Icons that were made before Windows had colors keep their see-through parts in a mask of their own.
                    ApplyMask(bits, mask, size);
                }

                Premultiply(bits);
                return bits;
            }
            finally
            {
                // The handles of the drawings of the icon belong to the caller, so they have to be given back.
                if (large != IntPtr.Zero)
                {
                    DestroyIcon(large);
                }

                if (small != IntPtr.Zero)
                {
                    DestroyIcon(small);
                }
            }
        }

        // Windows keeps the handles of the drawings of an icon in numbers of four bytes, even on a sixty four bit computer.
        private static IntPtr Handle(long value)
        {
            return new IntPtr(value & 0xFFFFFFFF);
        }

        private static byte[]? ReadBitmap(IntPtr bitmap, int size)
        {
            var header = NewHeader(size);
            var bits = new byte[size * size * 4];

            var deviceContext = GetDC(IntPtr.Zero);
            int lines;
            try
            {
                lines = GetDIBits(deviceContext, bitmap, 0, (uint)size, bits, ref header, 0);
            }
            finally
            {
                // Nothing actionable if releasing the screen device fails.
                _ = ReleaseDC(IntPtr.Zero, deviceContext);
            }

            if (lines <= 0)
            {
                return null;
            }

            return bits;
        }

        private static void ApplyMask(byte[] bits, IntPtr mask, int size)
        {
            var maskBits = ReadBitmap(mask, size);
            if (maskBits == null)
            {
                // Without a mask the icon is shown as a plain square.
                for (var i = 3; i < bits.Length; i += 4)
                {
                    bits[i] = 255;
                }

                return;
            }

            for (var i = 0; i < bits.Length; i += 4)
            {
                // The mask is black where the picture is shown and white where it is hidden.
                bits[i + 3] = maskBits[i] > 128 ? (byte)0 : (byte)255;
            }
        }

        private static bool HasAlpha(byte[] bits)
        {
            for (var i = 3; i < bits.Length; i += 4)
            {
                if (bits[i] > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static void Premultiply(byte[] bits)
        {
            for (var i = 0; i < bits.Length; i += 4)
            {
                var alpha = bits[i + 3];
                if (alpha == 0)
                {
                    bits[i] = 0;
                    bits[i + 1] = 0;
                    bits[i + 2] = 0;
                    continue;
                }

                if (alpha == 255)
                {
                    continue;
                }

                bits[i] = (byte)(bits[i] * alpha / 255);
                bits[i + 1] = (byte)(bits[i + 1] * alpha / 255);
                bits[i + 2] = (byte)(bits[i + 2] * alpha / 255);
            }
        }

        private static BitmapHeader NewHeader(int size)
        {
            return new BitmapHeader
            {
                Size = (uint)Marshal.SizeOf<BitmapHeader>(),
                Width = size,
                Height = -size,
                Planes = 1,
                BitsPerPixel = 32,
                Compression = 0,
            };
        }
    }
}
