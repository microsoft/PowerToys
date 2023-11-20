// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers.Binary;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

//// Based on https://github.com/phoboslab/qoi/blob/master/qoi.h

namespace Microsoft.PowerToys.FilePreviewCommon
{
    /// <summary>
    /// QOI Image helper.
    /// </summary>
    public static class QoiImage
    {
#pragma warning disable SA1310 // Field names should not contain underscore
        private const byte QOI_OP_INDEX = 0x00; // 00xxxxxx
        private const byte QOI_OP_DIFF = 0x40;  // 01xxxxxx
        private const byte QOI_OP_LUMA = 0x80;  // 10xxxxxx
        private const byte QOI_OP_RUN = 0xc0;   // 11xxxxxx
        private const byte QOI_OP_RGB = 0xfe;   // 11111110
        private const byte QOI_OP_RGBA = 0xff;  // 11111111

        private const byte QOI_MASK_2 = 0xc0;   // 11000000

        private const int QOI_MAGIC = 'q' << 24 | 'o' << 16 | 'i' << 8 | 'f';
        private const int QOI_HEADER_SIZE = 14;

        private const uint QOI_PIXELS_MAX = 400000000;

        private const byte QOI_PADDING_LENGTH = 8;
#pragma warning restore SA1310 // Field names should not contain underscore

        private record struct QoiPixel(byte R, byte G, byte B, byte A)
        {
            public readonly int GetColorHash() => (R * 3) + (G * 5) + (B * 7) + (A * 11);
        }

        /// <summary>
        /// Creates a <see cref="Bitmap"/> from the specified QOI data stream.
        /// </summary>
        /// <param name="stream">A <see cref="Stream"/> that contains the QOI data.</param>
        /// <returns>The <see cref="Bitmap"/> this method creates.</returns>
        /// <exception cref="ArgumentException">The stream does not have a valid QOI image format.</exception>
        public static Bitmap FromStream(Stream stream)
        {
            var fileSize = stream.Length;

            if (fileSize < QOI_HEADER_SIZE + QOI_PADDING_LENGTH)
            {
                throw new ArgumentException("Not enough data for a QOI file");
            }

            Bitmap? bitmap = null;

            try
            {
                using var reader = new BinaryReader(stream, Encoding.UTF8, true);

                var headerMagic = ReadUInt32BigEndian(reader);

                if (headerMagic != QOI_MAGIC)
                {
                    throw new ArgumentException("Invalid QOI file header");
                }

                var width = ReadUInt32BigEndian(reader);
                var height = ReadUInt32BigEndian(reader);
                var channels = reader.ReadByte();
                var colorSpace = reader.ReadByte();

                if (width == 0 || height == 0 || channels < 3 || channels > 4 || colorSpace > 1 || height >= QOI_PIXELS_MAX / width)
                {
                    throw new ArgumentException("Invalid QOI file data");
                }

                var pixelFormat = channels == 4 ? PixelFormat.Format32bppArgb : PixelFormat.Format24bppRgb;

                bitmap = new Bitmap((int)width, (int)height, pixelFormat);

                var bitmapData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.WriteOnly, pixelFormat);
                var dataLength = bitmapData.Height * bitmapData.Stride;

                var index = new QoiPixel[64];
                var pixel = new QoiPixel(0, 0, 0, 255);

                var run = 0;
                var chunksLen = fileSize - QOI_PADDING_LENGTH;

                for (var dataIndex = 0; dataIndex < dataLength; dataIndex += channels)
                {
                    if (run > 0)
                    {
                        run--;
                    }
                    else if (stream.Position < chunksLen)
                    {
                        var b1 = reader.ReadByte();

                        if (b1 == QOI_OP_RGB)
                        {
                            pixel.R = reader.ReadByte();
                            pixel.G = reader.ReadByte();
                            pixel.B = reader.ReadByte();
                        }
                        else if (b1 == QOI_OP_RGBA)
                        {
                            pixel.R = reader.ReadByte();
                            pixel.G = reader.ReadByte();
                            pixel.B = reader.ReadByte();
                            pixel.A = reader.ReadByte();
                        }
                        else if ((b1 & QOI_MASK_2) == QOI_OP_INDEX)
                        {
                            pixel = index[b1];
                        }
                        else if ((b1 & QOI_MASK_2) == QOI_OP_DIFF)
                        {
                            pixel.R += (byte)(((b1 >> 4) & 0x03) - 2);
                            pixel.G += (byte)(((b1 >> 2) & 0x03) - 2);
                            pixel.B += (byte)((b1 & 0x03) - 2);
                        }
                        else if ((b1 & QOI_MASK_2) == QOI_OP_LUMA)
                        {
                            var b2 = reader.ReadByte();
                            var vg = (b1 & 0x3f) - 32;
                            pixel.R += (byte)(vg - 8 + ((b2 >> 4) & 0x0f));
                            pixel.G += (byte)vg;
                            pixel.B += (byte)(vg - 8 + (b2 & 0x0f));
                        }
                        else if ((b1 & QOI_MASK_2) == QOI_OP_RUN)
                        {
                            run = b1 & 0x3f;
                        }

                        index[pixel.GetColorHash() % 64] = pixel;
                    }

                    unsafe
                    {
                        var bitmapPixel = (byte*)bitmapData.Scan0 + dataIndex;

                        bitmapPixel[0] = pixel.B;
                        bitmapPixel[1] = pixel.G;
                        bitmapPixel[2] = pixel.R;
                        if (channels == 4)
                        {
                            bitmapPixel[3] = pixel.A;
                        }
                    }
                }

                bitmap.UnlockBits(bitmapData);

                return bitmap;
            }
            catch
            {
                bitmap?.Dispose();

                throw;
            }
        }

        private static uint ReadUInt32BigEndian(BinaryReader reader)
        {
            var buffer = reader.ReadBytes(4);

            return BinaryPrimitives.ReadUInt32BigEndian(buffer);
        }
    }
}
