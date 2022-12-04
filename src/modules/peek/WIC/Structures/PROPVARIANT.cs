using System;
using System.Runtime.InteropServices;

namespace WIC
{
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct PROPVARIANT
    {
        public VARTYPE Type;
        public PROPVARIANT_Value Value;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct PROPVARIANT_Value
    {
        [FieldOffset(0)]
        public sbyte I1;

        [FieldOffset(0)]
        public byte UI1;

        [FieldOffset(0)]
        public short I2;

        [FieldOffset(0)]
        public ushort UI2;

        [FieldOffset(0)]
        public int I4;

        [FieldOffset(0)]
        public uint UI4;

        [FieldOffset(0)]
        public long I8;

        [FieldOffset(0)]
        public ulong UI8;

        [FieldOffset(0)]
        public PROPVARIANT_SplitI8 SplitI8;

        [FieldOffset(0)]
        public PROPVARIANT_SplitUI8 SplitUI8;

        [FieldOffset(0)]
        public float R4;

        [FieldOffset(0)]
        public double R8;

        [FieldOffset(0)]
        public IntPtr Ptr;

        [FieldOffset(0)]
        public PROPVARIANT_Vector Vector;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct PROPVARIANT_SplitI8
    {
        public int A;
        public int B;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct PROPVARIANT_SplitUI8
    {
        public uint A;
        public uint B;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PROPVARIANT_Vector
    {
        public int Length;
        public IntPtr Ptr;
    }
}
