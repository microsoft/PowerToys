// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace ImageResizer.Utilities
{
    internal static class MetadataTagIds
    {
        internal static class Ifd
        {
            public const ushort ImageWidth = 256;
            public const ushort ImageHeight = 257;
            public const ushort Make = 271;
            public const ushort Model = 272;
            public const ushort StripOffsets = 273;
            public const ushort Orientation = 274;
            public const ushort DateTime = 306;
            public const ushort Artist = 315;
            public const ushort StripByteCounts = 279;
            public const ushort TileOffsets = 324;
            public const ushort TileByteCounts = 325;
            public const ushort ThumbnailOffset = 513;
            public const ushort ThumbnailLength = 514;
            public const ushort Copyright = 33432;
            public const ushort ExifIfdPointer = 34665;
            public const ushort GpsIfdPointer = 34853;
        }

        internal static class Exif
        {
            public const ushort ExposureTime = 33434;
            public const ushort FNumber = 33437;
            public const ushort DateTakenOriginal = 36867;
            public const ushort DateTakenDigitized = 36868;
            public const ushort ExposureBias = 37380;
            public const ushort MeteringMode = 37383;
            public const ushort Flash = 37385;
            public const ushort FocalLength = 37386;
            public const ushort MakerNote = 37500;
            public const ushort PixelXDimension = 40962;
            public const ushort PixelYDimension = 40963;
            public const ushort ColorSpace = 40961;
            public const ushort InteropIfdPointer = 40965;
            public const ushort WhiteBalance = 41987;
            public const ushort LensModel = 42036;
            public const ushort IsoSpeed = 34855;
        }

        internal static class Gps
        {
            public const ushort VersionId = 0;
            public const ushort LatitudeRef = 1;
            public const ushort Latitude = 2;
            public const ushort LongitudeRef = 3;
            public const ushort Longitude = 4;
            public const ushort AltitudeRef = 5;
            public const ushort Altitude = 6;
        }
    }
}
