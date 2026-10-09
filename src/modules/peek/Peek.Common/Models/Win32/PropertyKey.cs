// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Peek.Common.Models
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct PropertyKey
    {
        public Guid FormatId;
        public int PropertyId;

        public PropertyKey(Guid keyGuid, int propertyId)
        {
            this.FormatId = keyGuid;
            this.PropertyId = propertyId;
        }

        public PropertyKey(uint a, uint b, uint c, uint d, uint e, uint f, uint g, uint h, uint i, uint j, uint k, int propertyId)
            : this(new Guid((uint)a, (ushort)b, (ushort)c, (byte)d, (byte)e, (byte)f, (byte)g, (byte)h, (byte)i, (byte)j, (byte)k), propertyId)
        {
        }

        public override bool Equals(object? obj)
        {
            if ((obj == null) || !(obj is PropertyKey))
            {
                return false;
            }

            PropertyKey pk = (PropertyKey)obj;

            return FormatId.Equals(pk.FormatId) && (PropertyId == pk.PropertyId);
        }

        public static bool operator ==(PropertyKey a, PropertyKey b)
        {
            if (((object)a == null) || ((object)b == null))
            {
                return false;
            }

            return a.FormatId == b.FormatId && a.PropertyId == b.PropertyId;
        }

        public static bool operator !=(PropertyKey a, PropertyKey b)
        {
            return !(a == b);
        }

        public override int GetHashCode()
        {
            return FormatId.GetHashCode() ^ PropertyId;
        }

        // File properties: https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-wsp/2dbe759c-c955-4770-a545-e46d7f6332ed
        public static readonly PropertyKey ImageHorizontalSize = new PropertyKey(new Guid(0x6444048F, 0x4C8B, 0x11D1, 0x8B, 0x70, 0x08, 0x00, 0x36, 0xB1, 0x1A, 0x03), 3);
        public static readonly PropertyKey ImageVerticalSize = new PropertyKey(new Guid(0x6444048F, 0x4C8B, 0x11D1, 0x8B, 0x70, 0x08, 0x00, 0x36, 0xB1, 0x1A, 0x03), 4);
        public static readonly PropertyKey FileSizeBytes = new PropertyKey(new Guid(0xb725f130, 0x47ef, 0x101a, 0xa5, 0xf1, 0x02, 0x60, 0x8c, 0x9e, 0xeb, 0xac), 12);
        public static readonly PropertyKey FileType = new PropertyKey(new Guid(0xb725f130, 0x47ef, 0x101a, 0xa5, 0xf1, 0x02, 0x60, 0x8c, 0x9e, 0xeb, 0xac), 4);
        public static readonly PropertyKey FrameWidth = new PropertyKey(new Guid(0x64440491, 0x4C8B, 0x11D1, 0x8B, 0x70, 0x08, 0x00, 0x36, 0xB1, 0x1A, 0x03), 3);
        public static readonly PropertyKey FrameHeight = new PropertyKey(new Guid(0x64440491, 0x4C8B, 0x11D1, 0x8B, 0x70, 0x08, 0x00, 0x36, 0xB1, 0x1A, 0x03), 4);
        public static readonly PropertyKey MusicTitle = new PropertyKey(new Guid(0xf29f85e0, 0x4ff9, 0x1068, 0xab, 0x91, 0x08, 0x00, 0x2b, 0x27, 0xb3, 0xd9), 2);
        public static readonly PropertyKey MusicDisplayArtist = new PropertyKey(new Guid(0xFD122953, 0xFA93, 0x4EF7, 0x92, 0xC3, 0x04, 0xC9, 0x46, 0xB2, 0xF7, 0xC8), 100);
        public static readonly PropertyKey MusicAlbum = new PropertyKey(new Guid(0x56a3372e, 0xce9c, 0x11d2, 0x9f, 0xe, 0x0, 0x60, 0x97, 0xc6, 0x86, 0xf6), 4);
        public static readonly PropertyKey MusicDuration = new PropertyKey(new Guid(0x64440490, 0x4c8b, 0x11d1, 0x8b, 0x70, 0x8, 0x0, 0x36, 0xb1, 0x1a, 0x3), 3);
    }
}
