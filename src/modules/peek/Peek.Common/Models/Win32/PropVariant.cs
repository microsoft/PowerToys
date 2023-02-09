// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Peek.Common.Models
{
    [StructLayout(LayoutKind.Explicit)]
    public struct PropVariant
    {
        [FieldOffset(0)]
        public short Vt;
        [FieldOffset(2)]
        public short WReserved1;
        [FieldOffset(4)]
        public short WReserved2;
        [FieldOffset(6)]
        public short WReserved3;
        [FieldOffset(8)]
        public sbyte CVal;
        [FieldOffset(8)]
        public byte BVal;
        [FieldOffset(8)]
        public short IVal;
        [FieldOffset(8)]
        public ushort UiVal;
        [FieldOffset(8)]
        public int LVal;
        [FieldOffset(8)]
        public uint UlVal;
        [FieldOffset(8)]
        public int IntVal;
        [FieldOffset(8)]
        public uint UintVal;
        [FieldOffset(8)]
        public long HVal;
        [FieldOffset(8)]
        public ulong UhVal;
        [FieldOffset(8)]
        public float FltVal;
        [FieldOffset(8)]
        public double DblVal;
        [FieldOffset(8)]
        public bool BoolVal;
        [FieldOffset(8)]
        public int Scode;
        [FieldOffset(8)]
        public DateTime Date;
        [FieldOffset(8)]
        public FileTime Filetime;
        [FieldOffset(8)]
        public Blob Blob;
        [FieldOffset(8)]
        public IntPtr P;
        [FieldOffset(8)]
        public CALPWSTR Calpwstr;
    }
}
