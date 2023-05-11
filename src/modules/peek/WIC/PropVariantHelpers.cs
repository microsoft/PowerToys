using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace WIC
{
    internal static class PropVariantHelpers
    {
        static PropVariantHelpers()
        {
            Func<PROPVARIANT, object> getComObject = variant =>
            {
                return Marshal.GetObjectForIUnknown(variant.Value.Ptr);
            };

            decoders = new Dictionary<VARTYPE, Func<PROPVARIANT, object>>()
            {
                [VARTYPE.VT_BOOL]    = variant => variant.Value.UI2 == 0 ? false : true,
                [VARTYPE.VT_UI1]     = variant => variant.Value.UI1,
                [VARTYPE.VT_UI2]     = variant => variant.Value.UI2,
                [VARTYPE.VT_UI4]     = variant => variant.Value.UI4,
                [VARTYPE.VT_UI8]     = variant => variant.Value.UI8,
                [VARTYPE.VT_I1]      = variant => variant.Value.I1,
                [VARTYPE.VT_I2]      = variant => variant.Value.I2,
                [VARTYPE.VT_I4]      = variant => variant.Value.I4,
                [VARTYPE.VT_I8]      = variant => variant.Value.I8,
                [VARTYPE.VT_LPSTR]   = variant => Marshal.PtrToStringAnsi(variant.Value.Ptr),
                [VARTYPE.VT_LPWSTR]  = variant => Marshal.PtrToStringUni(variant.Value.Ptr),
                [VARTYPE.VT_BSTR]    = variant => Marshal.PtrToStringBSTR(variant.Value.Ptr),
                [VARTYPE.VT_R4]      = variant => variant.Value.R4,
                [VARTYPE.VT_R8]      = variant => variant.Value.R8,
                [VARTYPE.VT_UNKNOWN] = variant => getComObject,
                [VARTYPE.VT_STREAM]  = variant => getComObject,
                [VARTYPE.VT_STORAGE] = variant => getComObject,
                [VARTYPE.VT_VECTOR]  = DecodeVector,
            };

            Action<PROPVARIANT> disposePtr = variant =>
            {
                Marshal.FreeCoTaskMem(variant.Value.Ptr);
            };
            Action<PROPVARIANT> disposeBSTR = variant =>
            {
                Marshal.FreeBSTR(variant.Value.Ptr);
            };
            Action<PROPVARIANT> disposeComObject = variant =>
            {
                Marshal.Release(variant.Value.Ptr);
            };

            disposers = new Dictionary<VARTYPE, Action<PROPVARIANT>>()
            {
                [VARTYPE.VT_LPSTR]   = disposePtr,
                [VARTYPE.VT_LPWSTR]  = disposePtr,
                [VARTYPE.VT_BSTR]    = disposeBSTR,
                [VARTYPE.VT_UNKNOWN] = disposeComObject,
                [VARTYPE.VT_STREAM]  = disposeComObject,
                [VARTYPE.VT_STORAGE] = disposeComObject,
                [VARTYPE.VT_VECTOR]  = DisposeVector,
            };
        }

        private static Dictionary<VARTYPE, Func<PROPVARIANT, object>> decoders;
        private static Dictionary<VARTYPE, Action<PROPVARIANT>> disposers;

        public static bool TryDecode<T>(ref PROPVARIANT variant, out T value)
        {
            const VARTYPE flagMask = VARTYPE.VT_ARRAY | VARTYPE.VT_VECTOR | VARTYPE.VT_BYREF;
            bool hasFlag = (variant.Type & flagMask) != (VARTYPE)0;
            Func<PROPVARIANT, object> decoder;
            if (decoders.TryGetValue(variant.Type, out decoder)
                || (hasFlag && decoders.TryGetValue(variant.Type & flagMask, out decoder)))
            {
                value = (T)decoder.Invoke(variant);
                return true;
            }
            else
            {
                value = default(T);
                return false;
            }
        }

        private static object DecodeVector(PROPVARIANT variant)
        {
            Type elementType;
            int elementSize;
            Func<IntPtr, object> elementDecoder;

            switch (variant.Type & ~VARTYPE.VT_VECTOR)
            {
                case VARTYPE.VT_I1:
                    elementType = typeof(sbyte);
                    elementDecoder = ptr => (object)(sbyte)Marshal.ReadByte(ptr);
                    elementSize = 1;
                    break;

                case VARTYPE.VT_I2:
                    elementType = typeof(short);
                    elementDecoder = ptr => (object)Marshal.ReadInt16(ptr);
                    elementSize = 2;
                    break;

                case VARTYPE.VT_I4:
                    elementType = typeof(int);
                    elementDecoder = ptr => (object)Marshal.ReadInt32(ptr);
                    elementSize = 4;
                    break;

                case VARTYPE.VT_I8:
                    elementType = typeof(long);
                    elementDecoder = ptr => (object)Marshal.ReadInt64(ptr);
                    elementSize = 8;
                    break;

                case VARTYPE.VT_UI1:
                    elementType = typeof(byte);
                    elementDecoder = ptr => (object)Marshal.ReadByte(ptr);
                    elementSize = 1;
                    break;

                case VARTYPE.VT_UI2:
                    elementType = typeof(ushort);
                    elementDecoder = ptr => (object)(ushort)Marshal.ReadInt16(ptr);
                    elementSize = 2;
                    break;

                case VARTYPE.VT_UI4:
                    elementType = typeof(uint);
                    elementDecoder = ptr => (object)(uint)Marshal.ReadInt32(ptr);
                    elementSize = 4;
                    break;

                case VARTYPE.VT_UI8:
                    elementType = typeof(ulong);
                    elementDecoder = ptr => (object)(ulong)Marshal.ReadInt64(ptr);
                    elementSize = 8;
                    break;

                case VARTYPE.VT_LPSTR:
                    elementType = typeof(string);
                    elementDecoder = Marshal.PtrToStringAnsi;
                    elementSize = IntPtr.Size;
                    break;

                case VARTYPE.VT_LPWSTR:
                    elementType = typeof(string);
                    elementDecoder = Marshal.PtrToStringUni;
                    elementSize = IntPtr.Size;
                    break;

                case VARTYPE.VT_UNKNOWN:
                case VARTYPE.VT_STREAM:
                case VARTYPE.VT_STORAGE:
                    elementType = typeof(object);
                    elementDecoder = Marshal.GetObjectForIUnknown;
                    elementSize = IntPtr.Size;
                    break;

                default:
                    #warning `PropVariantHelpers.DecodeVector` does not yet know how to handle some `PROPVARIANT` types.
                    throw new System.NotImplementedException();
            }

            int length = variant.Value.Vector.Length;
            var vector = Array.CreateInstance(elementType, length);
            IntPtr elementPtr = variant.Value.Vector.Ptr;
            for (int i = 0; i < length; ++i)
            {
                vector.SetValue(elementDecoder.Invoke(elementPtr), i);
                elementPtr += elementSize;
            }

            return vector;
        }

        public static void Dispose(ref PROPVARIANT variant)
        {
            const VARTYPE flagMask = VARTYPE.VT_ARRAY | VARTYPE.VT_VECTOR | VARTYPE.VT_BYREF;
            bool hasFlag = (variant.Type & flagMask) != (VARTYPE)0;
            Action<PROPVARIANT> disposer;
            if (disposers.TryGetValue(variant.Type, out disposer)
                || (hasFlag && disposers.TryGetValue(variant.Type & flagMask, out disposer)))
            {
                disposer.Invoke(variant);
            }
            variant = new PROPVARIANT();
        }

        private static void DisposeVector(PROPVARIANT variant)
        {
            Action<IntPtr> elementDisposer;
            int elementSize;

            switch (variant.Type & ~VARTYPE.VT_VECTOR)
            {
                case VARTYPE.VT_BOOL:
                case VARTYPE.VT_I1:
                case VARTYPE.VT_I2:
                case VARTYPE.VT_I4:
                case VARTYPE.VT_I8:
                case VARTYPE.VT_UI1:
                case VARTYPE.VT_UI2:
                case VARTYPE.VT_UI4:
                case VARTYPE.VT_UI8:
                case VARTYPE.VT_R4:
                case VARTYPE.VT_R8:
                    elementDisposer = null;
                    elementSize = IntPtr.Size;
                    break;

                case VARTYPE.VT_BSTR:
                    elementDisposer = Marshal.FreeBSTR;
                    elementSize = IntPtr.Size;
                    break;

                case VARTYPE.VT_LPSTR:
                case VARTYPE.VT_LPWSTR:
                    elementDisposer = Marshal.FreeCoTaskMem;
                    elementSize = IntPtr.Size;
                    break;

                case VARTYPE.VT_UNKNOWN:
                case VARTYPE.VT_STREAM:
                case VARTYPE.VT_STORAGE:
                    elementDisposer = ptr => { int ignored = Marshal.Release(ptr); };
                    elementSize = IntPtr.Size;
                    break;

                default:
                    #warning `PropVariantHelpers.DisposeVector` does not yet know how to handle some `PROPVARIANT` types.
                    throw new System.NotImplementedException();
            }

            IntPtr vectorPtr = variant.Value.Vector.Ptr;

            // if necessary, dispose each of the vector's elements:
            if (elementDisposer != null)
            {
                IntPtr elementPtr = vectorPtr;
                for (int i = 0, n = variant.Value.Vector.Length; i < n; ++i)
                {
                    elementDisposer.Invoke(elementPtr);
                    elementPtr += elementSize;
                }
            }

            // finally, dispose the vector array itself:
            Marshal.FreeCoTaskMem(vectorPtr);
        }
    }
}
