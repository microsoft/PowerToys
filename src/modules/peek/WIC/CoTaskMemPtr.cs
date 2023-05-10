using System;
using System.Runtime.InteropServices;

namespace WIC
{
    internal struct CoTaskMemPtr : IDisposable
    {
        public static CoTaskMemPtr From<T>(T? nullableStructure) where T : struct
        {
            IntPtr value;
            if (nullableStructure.HasValue)
            {
                value = Marshal.AllocCoTaskMem(Marshal.SizeOf(typeof(T)));
                Marshal.StructureToPtr(nullableStructure, value, false);
            }
            else
            {
                value = IntPtr.Zero;
            }
            return new CoTaskMemPtr(value);
        }

        public CoTaskMemPtr(IntPtr value)
        {
            this.value = value;
        }

        private IntPtr value;

        public static implicit operator IntPtr(CoTaskMemPtr safeIntPtr)
        {
            return safeIntPtr.value;
        }

        public void Dispose()
        {
            if (value != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(value);
            }
        }
    }
}
