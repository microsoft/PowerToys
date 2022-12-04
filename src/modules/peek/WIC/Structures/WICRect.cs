using System.Runtime.InteropServices;

namespace WIC
{
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
    public struct WICRect
    {
        public int X;
        public int Y;
        public int Width;
        public int Height;
    }
}
