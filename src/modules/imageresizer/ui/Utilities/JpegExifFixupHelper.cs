// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text;

namespace ImageResizer.Utilities
{
    internal static class JpegExifFixupHelper
    {
        private static readonly byte[] ExifHeader = Encoding.ASCII.GetBytes("Exif\0\0");

        private const ushort TiffTypeByte = 1;
        private const ushort TiffTypeAscii = 2;
        private const ushort TiffTypeShort = 3;
        private const ushort TiffTypeLong = 4;
        private const ushort TiffTypeRational = 5;
        private const ushort TiffTypeUndefined = 7;
        private const ushort TiffTypeSLong = 9;
        private const ushort TiffTypeSRational = 10;
        private const ushort TiffTypeFloat = 11;
        private const ushort TiffTypeDouble = 12;

        public static bool TryRewriteExif(IFileSystem fileSystem, string sourcePath, string destinationPath, uint width, uint height)
        {
            if (fileSystem == null || string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(destinationPath))
            {
                return false;
            }

            try
            {
                var sourceBytes = fileSystem.File.ReadAllBytes(sourcePath);
                var destinationBytes = fileSystem.File.ReadAllBytes(destinationPath);

                if (!TryExtractExifPayload(sourceBytes, out var sourceExifPayload)
                    || !TryBuildRewrittenExifPayload(sourceExifPayload, width, height, out var rewrittenExifPayload)
                    || !TryReplaceExifSegment(destinationBytes, rewrittenExifPayload, out var rewrittenJpeg))
                {
                    return false;
                }

                fileSystem.File.WriteAllBytes(destinationPath, rewrittenJpeg);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"JPEG EXIF fix-up failed for '{destinationPath}': {ex}");
                return false;
            }
        }

        private static bool TryExtractExifPayload(byte[] jpegBytes, out byte[] exifPayload)
        {
            exifPayload = null;
            if (!TryParseJpegSegments(jpegBytes, out var segments, out _))
            {
                return false;
            }

            foreach (var segment in segments)
            {
                if (segment.Marker == 0xE1 && IsExifSegment(segment.Data))
                {
                    exifPayload = new byte[segment.Data.Length - ExifHeader.Length];
                    Buffer.BlockCopy(segment.Data, ExifHeader.Length, exifPayload, 0, exifPayload.Length);
                    return true;
                }
            }

            return false;
        }

        private static bool TryBuildRewrittenExifPayload(byte[] sourceExifPayload, uint width, uint height, out byte[] rewrittenExifPayload)
        {
            rewrittenExifPayload = null;
            if (sourceExifPayload == null || sourceExifPayload.Length < 8)
            {
                return false;
            }

            if (!TryParseTiff(sourceExifPayload, out var sourceRoot, out var littleEndian))
            {
                return false;
            }

            var rewrittenRoot = BuildRootIfd(sourceRoot, littleEndian, width, height);
            if (rewrittenRoot == null || rewrittenRoot.Entries.Count == 0)
            {
                return false;
            }

            var finalSize = AssignOffsets(rewrittenRoot, 8);
            var tiffBytes = new byte[finalSize];
            WriteTiffHeader(tiffBytes, littleEndian);
            WriteIfd(tiffBytes, rewrittenRoot, littleEndian);

            rewrittenExifPayload = new byte[ExifHeader.Length + tiffBytes.Length];
            Buffer.BlockCopy(ExifHeader, 0, rewrittenExifPayload, 0, ExifHeader.Length);
            Buffer.BlockCopy(tiffBytes, 0, rewrittenExifPayload, ExifHeader.Length, tiffBytes.Length);

            return true;
        }

        private static TiffIfd BuildRootIfd(TiffIfd sourceRoot, bool littleEndian, uint width, uint height)
        {
            var root = new TiffIfd();

            foreach (var entry in sourceRoot.Entries)
            {
                // Root IFD structural fields are rebuilt from the newly encoded image rather than
                // copied verbatim. Width/height, strip/tile offsets, thumbnail references, and
                // child-IFD pointers all depend on the rewritten container layout.
                if (entry.Tag is MetadataTagIds.Ifd.ImageWidth or
                    MetadataTagIds.Ifd.ImageHeight or
                    MetadataTagIds.Ifd.StripOffsets or
                    MetadataTagIds.Ifd.TileOffsets or
                    MetadataTagIds.Ifd.TileByteCounts or
                    MetadataTagIds.Ifd.ThumbnailOffset or
                    MetadataTagIds.Ifd.ThumbnailLength or
                    MetadataTagIds.Ifd.ExifIfdPointer or
                    MetadataTagIds.Ifd.GpsIfdPointer)
                {
                    continue;
                }

                if (entry.ChildIfd != null)
                {
                    continue;
                }

                root.Entries.Add(entry.CloneWithoutChild());
            }

            root.Entries.Add(CreateLongEntry(MetadataTagIds.Ifd.ImageWidth, width, littleEndian));
            root.Entries.Add(CreateLongEntry(MetadataTagIds.Ifd.ImageHeight, height, littleEndian));

            var exifSource = sourceRoot.GetChild(MetadataTagIds.Ifd.ExifIfdPointer);
            var exifChild = BuildExifIfd(exifSource, littleEndian, width, height);
            if (exifChild != null && exifChild.Entries.Count > 0)
            {
                root.Entries.Add(CreatePointerEntry(MetadataTagIds.Ifd.ExifIfdPointer, exifChild));
            }

            var gpsSource = sourceRoot.GetChild(MetadataTagIds.Ifd.GpsIfdPointer);
            if (gpsSource != null)
            {
                var gpsChild = CloneIfd(gpsSource);
                if (gpsChild.Entries.Count > 0)
                {
                    root.Entries.Add(CreatePointerEntry(MetadataTagIds.Ifd.GpsIfdPointer, gpsChild));
                }
            }

            return root;
        }

        private static TiffIfd BuildExifIfd(TiffIfd sourceExif, bool littleEndian, uint width, uint height)
        {
            if (sourceExif == null)
            {
                return null;
            }

            var exif = new TiffIfd();
            foreach (var entry in sourceExif.Entries)
            {
                // EXIF pixel dimensions must match the resized output, the Interop child is
                // rebuilt separately if present, and MakerNote is intentionally dropped because
                // its vendor-specific offsets are not reliable after rewriting.
                if (entry.Tag is MetadataTagIds.Exif.PixelXDimension or
                    MetadataTagIds.Exif.PixelYDimension or
                    MetadataTagIds.Exif.InteropIfdPointer or
                    MetadataTagIds.Exif.MakerNote)
                {
                    continue;
                }

                if (entry.ChildIfd != null)
                {
                    continue;
                }

                exif.Entries.Add(entry.CloneWithoutChild());
            }

            exif.Entries.Add(CreateLongEntry(MetadataTagIds.Exif.PixelXDimension, width, littleEndian));
            exif.Entries.Add(CreateLongEntry(MetadataTagIds.Exif.PixelYDimension, height, littleEndian));

            var interopSource = sourceExif.GetChild(MetadataTagIds.Exif.InteropIfdPointer);
            if (interopSource != null)
            {
                var interop = CloneIfd(interopSource);
                if (interop.Entries.Count > 0)
                {
                    exif.Entries.Add(CreatePointerEntry(MetadataTagIds.Exif.InteropIfdPointer, interop));
                }
            }

            return exif;
        }

        private static TiffIfd CloneIfd(TiffIfd source)
        {
            var clone = new TiffIfd();
            foreach (var entry in source.Entries)
            {
                if (entry.ChildIfd != null)
                {
                    var childClone = CloneIfd(entry.ChildIfd);
                    if (childClone.Entries.Count > 0)
                    {
                        clone.Entries.Add(CreatePointerEntry(entry.Tag, childClone));
                    }
                }
                else
                {
                    clone.Entries.Add(entry.CloneWithoutChild());
                }
            }

            return clone;
        }

        private static int AssignOffsets(TiffIfd ifd, int startOffset)
        {
            ifd.Offset = AlignEven(startOffset);
            var entries = ifd.GetOrderedEntries();

            // TIFF stores values larger than 4 bytes out-of-line and references them by offset
            // from the containing IFD entry. We also assign offsets for child IFDs here so the
            // final serialized graph is deterministic and even-aligned.
            int cursor = ifd.Offset + 2 + (entries.Count * 12) + 4;

            foreach (var entry in entries)
            {
                if (entry.ChildIfd != null || entry.Data.Length <= 4)
                {
                    continue;
                }

                cursor = AlignEven(cursor);
                entry.AssignedOffset = cursor;
                cursor += entry.Data.Length;
            }

            foreach (var entry in entries.Where(entry => entry.ChildIfd != null))
            {
                cursor = AlignEven(cursor);
                entry.AssignedOffset = cursor;
                cursor = AssignOffsets(entry.ChildIfd, cursor);
            }

            return cursor;
        }

        private static void WriteTiffHeader(byte[] destination, bool littleEndian)
        {
            destination[0] = littleEndian ? (byte)'I' : (byte)'M';
            destination[1] = littleEndian ? (byte)'I' : (byte)'M';
            WriteUInt16(destination, 2, 42, littleEndian);
            WriteUInt32(destination, 4, 8, littleEndian);
        }

        private static void WriteIfd(byte[] destination, TiffIfd ifd, bool littleEndian)
        {
            var entries = ifd.GetOrderedEntries();
            WriteUInt16(destination, ifd.Offset, checked((ushort)entries.Count), littleEndian);

            int entryOffset = ifd.Offset + 2;
            foreach (var entry in entries)
            {
                WriteUInt16(destination, entryOffset, entry.Tag, littleEndian);
                WriteUInt16(destination, entryOffset + 2, entry.Type, littleEndian);
                WriteUInt32(destination, entryOffset + 4, entry.Count, littleEndian);

                if (entry.ChildIfd != null)
                {
                    WriteUInt32(destination, entryOffset + 8, checked((uint)entry.ChildIfd.Offset), littleEndian);
                }
                else if (entry.Data.Length <= 4)
                {
                    Array.Clear(destination, entryOffset + 8, 4);
                    Buffer.BlockCopy(entry.Data, 0, destination, entryOffset + 8, entry.Data.Length);
                }
                else
                {
                    WriteUInt32(destination, entryOffset + 8, checked((uint)entry.AssignedOffset), littleEndian);
                    Buffer.BlockCopy(entry.Data, 0, destination, entry.AssignedOffset, entry.Data.Length);
                }

                entryOffset += 12;
            }

            WriteUInt32(destination, entryOffset, 0, littleEndian);

            foreach (var child in entries.Where(entry => entry.ChildIfd != null))
            {
                WriteIfd(destination, child.ChildIfd, littleEndian);
            }
        }

        private static TiffEntry CreatePointerEntry(ushort tag, TiffIfd childIfd)
            => new()
            {
                Tag = tag,
                Type = TiffTypeLong,
                Count = 1,
                Data = Array.Empty<byte>(),
                ChildIfd = childIfd,
            };

        private static TiffEntry CreateLongEntry(ushort tag, uint value, bool littleEndian)
        {
            var data = new byte[4];
            WriteUInt32(data, 0, value, littleEndian);
            return new TiffEntry
            {
                Tag = tag,
                Type = TiffTypeLong,
                Count = 1,
                Data = data,
            };
        }

        private static bool TryParseTiff(byte[] tiffBytes, out TiffIfd rootIfd, out bool littleEndian)
        {
            rootIfd = null;
            littleEndian = true;

            if (tiffBytes.Length < 8)
            {
                return false;
            }

            if (tiffBytes[0] == (byte)'I' && tiffBytes[1] == (byte)'I')
            {
                littleEndian = true;
            }
            else if (tiffBytes[0] == (byte)'M' && tiffBytes[1] == (byte)'M')
            {
                littleEndian = false;
            }
            else
            {
                return false;
            }

            if (ReadUInt16(tiffBytes, 2, littleEndian) != 42)
            {
                return false;
            }

            uint rootOffset = ReadUInt32(tiffBytes, 4, littleEndian);
            var visited = new HashSet<uint>();
            rootIfd = ParseIfd(tiffBytes, rootOffset, littleEndian, visited);
            return rootIfd != null;
        }

        private static TiffIfd ParseIfd(byte[] tiffBytes, uint ifdOffset, bool littleEndian, HashSet<uint> visited)
        {
            if (ifdOffset == 0 || ifdOffset + 2 > tiffBytes.Length || !visited.Add(ifdOffset))
            {
                return null;
            }

            ushort entryCount = ReadUInt16(tiffBytes, checked((int)ifdOffset), littleEndian);
            int entriesStart = checked((int)ifdOffset + 2);
            int entriesLength = checked(entryCount * 12);
            if (entriesStart + entriesLength + 4 > tiffBytes.Length)
            {
                return null;
            }

            var ifd = new TiffIfd();
            for (int i = 0; i < entryCount; i++)
            {
                int entryOffset = entriesStart + (i * 12);
                ushort tag = ReadUInt16(tiffBytes, entryOffset, littleEndian);
                ushort type = ReadUInt16(tiffBytes, entryOffset + 2, littleEndian);
                uint count = ReadUInt32(tiffBytes, entryOffset + 4, littleEndian);
                uint valueOrOffset = ReadUInt32(tiffBytes, entryOffset + 8, littleEndian);
                int unitSize = GetTypeSize(type);
                if (unitSize <= 0)
                {
                    // Skip unsupported TIFF field types instead of failing the whole EXIF
                    // rewrite. Best-effort preservation is preferable here because an
                    // unknown vendor/app-specific entry should not prevent us from fixing
                    // the dimensions and keeping the rest of the metadata coherent.
                    continue;
                }

                ulong totalSize64 = (ulong)unitSize * count;
                if (totalSize64 > int.MaxValue)
                {
                    continue;
                }

                int totalSize = (int)totalSize64;
                byte[] data;
                if (totalSize <= 4)
                {
                    // Small TIFF values are stored inline in the entry's value/offset slot.
                    data = new byte[totalSize];
                    Buffer.BlockCopy(tiffBytes, entryOffset + 8, data, 0, totalSize);
                }
                else
                {
                    if (valueOrOffset + totalSize > tiffBytes.Length)
                    {
                        continue;
                    }

                    data = new byte[totalSize];
                    Buffer.BlockCopy(tiffBytes, checked((int)valueOrOffset), data, 0, totalSize);
                }

                TiffIfd childIfd = null;
                if (IsPointerTag(tag) && type == TiffTypeLong && count == 1)
                {
                    childIfd = ParseIfd(tiffBytes, valueOrOffset, littleEndian, visited);
                }

                ifd.Entries.Add(new TiffEntry
                {
                    Tag = tag,
                    Type = type,
                    Count = count,
                    Data = data,
                    ChildIfd = childIfd,
                });
            }

            // EXIF commonly links IFD1 through the trailing next-IFD pointer for thumbnails.
            // The resize rewrite intentionally rebuilds only IFD0 plus explicit Exif/GPS/Interop
            // children so stale thumbnail structures are dropped instead of being carried forward.
            return ifd;
        }

        private static bool TryReplaceExifSegment(byte[] destinationBytes, byte[] exifPayload, out byte[] rewrittenJpeg)
        {
            rewrittenJpeg = null;
            if (!TryParseJpegSegments(destinationBytes, out var segments, out int imageDataOffset))
            {
                return false;
            }

            if (ExifHeader.Length + exifPayload.Length > ushort.MaxValue - 2)
            {
                return false;
            }

            using var output = new MemoryStream(destinationBytes.Length + exifPayload.Length + 32);
            output.WriteByte(0xFF);
            output.WriteByte(0xD8);

            bool insertedExif = false;
            int index = 0;

            // Keep leading APP0/JFIF-style segments in place, replace only the EXIF APP1 block,
            // and copy the byte stream from SOS onward unchanged so scan data is untouched.
            while (index < segments.Count && segments[index].Marker == 0xE0)
            {
                WriteSegment(output, segments[index]);
                index++;
            }

            WriteExifSegment(output, exifPayload);
            insertedExif = true;

            for (; index < segments.Count; index++)
            {
                if (segments[index].Marker == 0xE1 && IsExifSegment(segments[index].Data))
                {
                    continue;
                }

                WriteSegment(output, segments[index]);
            }

            if (!insertedExif)
            {
                WriteExifSegment(output, exifPayload);
            }

            output.Write(destinationBytes, imageDataOffset, destinationBytes.Length - imageDataOffset);
            rewrittenJpeg = output.ToArray();
            return true;
        }

        private static bool TryParseJpegSegments(byte[] jpegBytes, out List<JpegSegment> segments, out int imageDataOffset)
        {
            segments = null;
            imageDataOffset = 0;

            if (jpegBytes.Length < 4 || jpegBytes[0] != 0xFF || jpegBytes[1] != 0xD8)
            {
                return false;
            }

            segments = [];
            int offset = 2;
            while (offset + 1 < jpegBytes.Length)
            {
                // Valid segments must start with 0xFF.
                if (jpegBytes[offset] != 0xFF)
                {
                    return false;
                }

                // Skip any padding 0xFF bytes to find the actual marker. We keep the
                // start of the full marker run so the later tail copy preserves any
                // legal padding bytes that appeared before SOS/EOI instead of normalizing
                // them away.
                int markerRunOffset = offset;
                while (offset < jpegBytes.Length && jpegBytes[offset] == 0xFF)
                {
                    offset++;
                }

                if (offset >= jpegBytes.Length)
                {
                    break;
                }

                // Check for Start of Scan (SOS) or End of Image (EOI).
                byte marker = jpegBytes[offset];
                if (marker == 0xDA || marker == 0xD9)
                {
                    imageDataOffset = markerRunOffset;
                    return true;
                }

                // Standalone markers with no length.
                if (marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7))
                {
                    segments.Add(new JpegSegment(marker, [], false));
                    offset++;
                    continue;
                }

                // Segments with a length field.
                if (offset + 2 >= jpegBytes.Length)
                {
                    return false;
                }

                // The length field includes its own 2 bytes, so it must be at least 2 to
                // be valid.
                ushort segmentLength = ReadUInt16BigEndian(jpegBytes, offset + 1);
                if (segmentLength < 2 || offset + 1 + segmentLength > jpegBytes.Length)
                {
                    return false;
                }

                var data = new byte[segmentLength - 2];
                Buffer.BlockCopy(jpegBytes, offset + 3, data, 0, data.Length);
                segments.Add(new JpegSegment(marker, data, true));

                offset += 1 + segmentLength;
            }

            return false;
        }

        private static void WriteSegment(Stream output, JpegSegment segment)
        {
            output.WriteByte(0xFF);
            output.WriteByte(segment.Marker);
            if (!segment.HasLength)
            {
                return;
            }

            ushort length = checked((ushort)(segment.Data.Length + 2));
            output.WriteByte((byte)(length >> 8));
            output.WriteByte((byte)(length & 0xFF));
            output.Write(segment.Data, 0, segment.Data.Length);
        }

        private static void WriteExifSegment(Stream output, byte[] exifPayload)
        {
            output.WriteByte(0xFF);
            output.WriteByte(0xE1);
            ushort length = checked((ushort)(exifPayload.Length + 2));
            output.WriteByte((byte)(length >> 8));
            output.WriteByte((byte)(length & 0xFF));
            output.Write(exifPayload, 0, exifPayload.Length);
        }

        private static bool IsExifSegment(byte[] data)
        {
            if (data == null || data.Length < ExifHeader.Length)
            {
                return false;
            }

            for (int i = 0; i < ExifHeader.Length; i++)
            {
                if (data[i] != ExifHeader[i])
                {
                    return false;
                }
            }

            return true;
        }

        // NB: only the EXIF/GPS/Interop pointers are traversed. The trailing next-IFD pointer is
        // intentionally ignored so we do not preserve stale thumbnail-oriented IFD1 data.
        private static bool IsPointerTag(ushort tag) =>
            tag is MetadataTagIds.Ifd.ExifIfdPointer or
            MetadataTagIds.Ifd.GpsIfdPointer or
            MetadataTagIds.Exif.InteropIfdPointer;

        private static int GetTypeSize(ushort type)
            => type switch
            {
                TiffTypeByte => 1,
                TiffTypeAscii => 1,
                TiffTypeShort => 2,
                TiffTypeLong => 4,
                TiffTypeRational => 8,
                TiffTypeUndefined => 1,
                TiffTypeSLong => 4,
                TiffTypeSRational => 8,
                TiffTypeFloat => 4,
                TiffTypeDouble => 8,
                _ => 0,
            };

        private static int AlignEven(int value)
            => (value + 1) & ~1;

        private static ushort ReadUInt16(byte[] bytes, int offset, bool littleEndian)
            => littleEndian
                ? (ushort)(bytes[offset] | (bytes[offset + 1] << 8))
                : (ushort)((bytes[offset] << 8) | bytes[offset + 1]);

        private static uint ReadUInt32(byte[] bytes, int offset, bool littleEndian)
            => littleEndian
                ? (uint)(bytes[offset]
                    | (bytes[offset + 1] << 8)
                    | (bytes[offset + 2] << 16)
                    | (bytes[offset + 3] << 24))
                : (uint)((bytes[offset] << 24)
                    | (bytes[offset + 1] << 16)
                    | (bytes[offset + 2] << 8)
                    | bytes[offset + 3]);

        // JPEG marker segment lengths are always stored in big-endian order, unlike the
        // embedded TIFF payload where byte order is selected by the file header.
        private static ushort ReadUInt16BigEndian(byte[] bytes, int offset)
            => (ushort)((bytes[offset] << 8) | bytes[offset + 1]);

        private static void WriteUInt16(byte[] bytes, int offset, ushort value, bool littleEndian)
        {
            if (littleEndian)
            {
                bytes[offset] = (byte)(value & 0xFF);
                bytes[offset + 1] = (byte)(value >> 8);
            }
            else
            {
                bytes[offset] = (byte)(value >> 8);
                bytes[offset + 1] = (byte)(value & 0xFF);
            }
        }

        private static void WriteUInt32(byte[] bytes, int offset, uint value, bool littleEndian)
        {
            if (littleEndian)
            {
                bytes[offset] = (byte)(value & 0xFF);
                bytes[offset + 1] = (byte)((value >> 8) & 0xFF);
                bytes[offset + 2] = (byte)((value >> 16) & 0xFF);
                bytes[offset + 3] = (byte)((value >> 24) & 0xFF);
            }
            else
            {
                bytes[offset] = (byte)((value >> 24) & 0xFF);
                bytes[offset + 1] = (byte)((value >> 16) & 0xFF);
                bytes[offset + 2] = (byte)((value >> 8) & 0xFF);
                bytes[offset + 3] = (byte)(value & 0xFF);
            }
        }

        private sealed class TiffIfd
        {
            public List<TiffEntry> Entries { get; } = new();

            public int Offset { get; set; }

            public TiffIfd GetChild(ushort tag)
                => Entries.FirstOrDefault(entry => entry.Tag == tag)?.ChildIfd;

            public List<TiffEntry> GetOrderedEntries()
                => Entries.OrderBy(entry => entry.Tag).ToList();
        }

        private sealed class TiffEntry
        {
            public ushort Tag { get; init; }

            public ushort Type { get; init; }

            public uint Count { get; init; }

            public byte[] Data { get; init; }

            public TiffIfd ChildIfd { get; init; }

            public int AssignedOffset { get; set; }

            public TiffEntry CloneWithoutChild()
                => new()
                {
                    Tag = Tag,
                    Type = Type,
                    Count = Count,
                    Data = Data?.ToArray() ?? Array.Empty<byte>(),
                };
        }

        private readonly record struct JpegSegment(byte Marker, byte[] Data, bool HasLength);
    }
}
