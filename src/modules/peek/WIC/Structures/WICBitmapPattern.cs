using System;
using System.Runtime.InteropServices;

namespace WIC
{
    public sealed class WICBitmapPattern
    {
        public long Position;
        public int Length;
        public byte[] Pattern;
        public byte[] Mask;
        public bool EndOfStream;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal sealed class WICBitmapPatternRaw
    {
        public long Position;
        public int Length;
        public IntPtr Pattern;
        public IntPtr Mask;
        public bool EndOfStream;
    }
}
