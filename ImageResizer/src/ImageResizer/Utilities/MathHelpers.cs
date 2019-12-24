using System;

namespace ImageResizer.Utilities
{
    static class MathHelpers
    {
        public static int Clamp(int value, int min, int max)
            => Math.Min(Math.Max(value, min), max);
    }
}
