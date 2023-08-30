// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// <summary>
//     Customed bitmap parser.
// </summary>
// <history>
//     2008 created by Truong Do (ductdo).
//     2009-... modified by Truong Do (TruongDo).
//     2023- Included in PowerToys.
// </history>
using System;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

[module: SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Scope = "member", Target = "MouseWithoutBorders.MyKnownBitmap.#FromFile(System.string)", Justification = "Dotnet port with style preservation")]

namespace MouseWithoutBorders.Class
{
#if CUSTOMIZE_LOGON_SCREEN
    internal class MyKnownBitmap
    {
        private const int BITMAP_FILE_HEADER_SIZE = 14;

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = BITMAP_FILE_HEADER_SIZE)]
        private struct BITMAPFILEHEADER
        {
            public ushort BfType;
            public uint BfSize;
            public ushort BfReserved1;
            public ushort BfReserved2;
            public uint BfOffBits;
        }

        private const int BITMAP_INFO_HEADER_SIZE = 40;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct BITMAPINFOHEADER
        {
            public uint BiSize;
            public int BiWidth;
            public int BiHeight;
            public ushort BiPlanes;
            public ushort BiBitCount;
            public uint BiCompression;
            public uint BiSizeImage;
            public int BiXPelsPerMeter;
            public int BiYPelsPerMeter;
            public uint BiClrUsed;
            public uint BiClrImportant;
        }

        private const long MAX_FILE_SIZE = 10 * 1024 * 1024;

        internal static object FromFile(string bitmapFile)
        {
            IntPtr p;
            byte[] buf;

            try
            {
                FileStream f = File.OpenRead(bitmapFile);
                long fs = f.Length;

                if (fs is < 1024 or > MAX_FILE_SIZE)
                {
                    f.Close();
                    return string.Format(CultureInfo.CurrentCulture, "File Size exception: {0}", fs);
                }

                BITMAPFILEHEADER bitmapFileHeader;
                buf = new byte[BITMAP_FILE_HEADER_SIZE];
                p = Marshal.AllocHGlobal(BITMAP_FILE_HEADER_SIZE);

                if (buf == null)
                {
                    f.Close();
                    return "p or buf is null! (1)";
                }

                _ = f.Read(buf, 0, buf.Length);
                Marshal.Copy(buf, 0, p, buf.Length);
                bitmapFileHeader = (BITMAPFILEHEADER)Marshal.PtrToStructure(p, typeof(BITMAPFILEHEADER));
                Marshal.FreeHGlobal(p);

                BITMAPINFOHEADER bitmapInfoHeader;
                buf = new byte[BITMAP_INFO_HEADER_SIZE];
                p = Marshal.AllocHGlobal(BITMAP_INFO_HEADER_SIZE);

                if (buf == null)
                {
                    f.Close();
                    return "p or buf is null! (2)";
                }

                _ = f.Read(buf, 0, buf.Length);
                Marshal.Copy(buf, 0, p, buf.Length);
                bitmapInfoHeader = (BITMAPINFOHEADER)Marshal.PtrToStructure(p, typeof(BITMAPINFOHEADER));
                Marshal.FreeHGlobal(p);

                if (bitmapFileHeader.BfType != 0x4D42 || bitmapFileHeader.BfSize != fs || bitmapFileHeader.BfOffBits != BITMAP_FILE_HEADER_SIZE + BITMAP_INFO_HEADER_SIZE ||
                    bitmapInfoHeader.BiBitCount != 24 || bitmapInfoHeader.BiCompression != 0 || bitmapInfoHeader.BiSize != BITMAP_INFO_HEADER_SIZE || bitmapInfoHeader.BiPlanes != 1 ||
                    bitmapInfoHeader.BiWidth <= 0 || bitmapInfoHeader.BiWidth > 4096 ||
                    bitmapInfoHeader.BiHeight <= 0 || bitmapInfoHeader.BiHeight > 4096)
                {
                    f.Close();
                    return string.Format(
                        CultureInfo.CurrentCulture,
                        "Bitmap Format Exception: {0} {1} {2} {3} {4} {5} {6} {7} {8} {9} {10}",
                        bitmapFileHeader.BfType,
                        bitmapFileHeader.BfSize,
                        bitmapFileHeader.BfOffBits,
                        bitmapInfoHeader.BiBitCount,
                        bitmapInfoHeader.BiCompression,
                        bitmapInfoHeader.BiSize,
                        bitmapInfoHeader.BiPlanes,
                        bitmapInfoHeader.BiWidth,
                        bitmapInfoHeader.BiWidth,
                        bitmapInfoHeader.BiHeight,
                        bitmapInfoHeader.BiHeight);
                }

                Bitmap bm = new(bitmapInfoHeader.BiWidth, bitmapInfoHeader.BiHeight, PixelFormat.Format24bppRgb);
                BitmapData bd = bm.LockBits(new Rectangle(0, 0, bm.Width, bm.Height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
                buf = new byte[bm.Width * bm.Height * 3];
                _ = f.Read(buf, 0, buf.Length);
                f.Close();
                int ws = buf.Length < (bd.Width * bd.Stride) ? buf.Length : bd.Width * bd.Stride;

                // Should never happen
                if (ws <= 0)
                {
                    bm.UnlockBits(bd);
                    bm.Dispose();
                    return string.Format(CultureInfo.CurrentCulture, "Something wrong: {0} {1} {2}", buf.Length, bd.Width, bd.Stride);
                }

                Marshal.Copy(buf, 0, bd.Scan0, ws);
                bm.UnlockBits(bd);
                bm.RotateFlip(RotateFlipType.Rotate180FlipX);
                return bm;
            }
            catch (Exception e)
            {
                return e.Message + e.StackTrace;
            }
        }

        private MyKnownBitmap()
        {
        }
    }
#endif
}
