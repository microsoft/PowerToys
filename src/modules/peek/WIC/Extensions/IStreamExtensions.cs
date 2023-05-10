using System;
using System.ComponentModel;
using Marshal = System.Runtime.InteropServices.Marshal;

namespace WIC
{
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static class IStreamExtensions
    {
        public static void Read(this IStream stream, byte[] pv, int cb)
        {
            stream.Read(pv, cb, IntPtr.Zero);
        }

        public static void Read(this IStream stream, byte[] pv, int cb, out int pcbRead)
        {
            var pcbReadPtr = Marshal.AllocCoTaskMem(4);
            try
            {
                stream.Read(pv, cb, pcbReadPtr);
                pcbRead = Marshal.ReadInt32(pcbReadPtr);
            }
            finally
            {
                Marshal.FreeCoTaskMem(pcbReadPtr);
            }
        }

        public static void Seek(this IStream stream, long dlibMove, STREAM_SEEK dwOrigin)
        {
            stream.Seek(dlibMove, dwOrigin, IntPtr.Zero);
        }

        public static void Seek(this IStream stream, long dlibMove, STREAM_SEEK dwOrigin, out long plibNewPosition)
        {
            var plibNewPositionPtr = Marshal.AllocCoTaskMem(8);
            try
            {
                stream.Seek(dlibMove, dwOrigin, plibNewPositionPtr);
                plibNewPosition = Marshal.ReadInt64(plibNewPositionPtr, 0);
            }
            finally
            {
                Marshal.FreeCoTaskMem(plibNewPositionPtr);
            }
        }

        public static void Write(this IStream stream, byte[] pv, int cb)
        {
            stream.Write(pv, cb, IntPtr.Zero);
        }

        public static void Write(this IStream stream, byte[] pv, int cb, out int pcbWritten)
        {
            var pcbWrittenPtr = Marshal.AllocCoTaskMem(4);
            try
            {
                stream.Read(pv, cb, pcbWrittenPtr);
                pcbWritten = Marshal.ReadInt32(pcbWrittenPtr);
            }
            finally
            {
                Marshal.FreeCoTaskMem(pcbWrittenPtr);
            }
        }
    }
}
