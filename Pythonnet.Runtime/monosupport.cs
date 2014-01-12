// ==========================================================================
// This software is subject to the provisions of the Zope Public License,
// Version 2.0 (ZPL).  A copy of the ZPL should accompany this distribution.
// THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
// WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
// FOR A PARTICULAR PURPOSE.
// ==========================================================================

#if (UCS4)
using System;
using System.Runtime.InteropServices;
using System.Text;
using Mono.Unix;

namespace Python.Runtime {
        // The Utf32Marshaler was written Jonathan Pryor and has been placed
        // in the PUBLIC DOMAIN. 
        public class Utf32Marshaler : ICustomMarshaler {
                private static Utf32Marshaler instance = new
                Utf32Marshaler ();
                
                public static ICustomMarshaler GetInstance (string s)
                {
                        return instance;
                }
                
                public void CleanUpManagedData (object o)
                {
                }
                
                public void CleanUpNativeData (IntPtr pNativeData)
                {
                        UnixMarshal.FreeHeap (pNativeData);
                }
                
                public int GetNativeDataSize ()
                {
                        return IntPtr.Size;
                }
                
                public IntPtr MarshalManagedToNative (object obj)
                {
                        string s = obj as string;
                        if (s == null)
                                return IntPtr.Zero;
                        return UnixMarshal.StringToHeap (s,
                        Encoding.UTF32);
                }
                
                public object MarshalNativeToManaged (IntPtr
                pNativeData)
                {
                        return UnixMarshal.PtrToString (pNativeData, 
                                Encoding.UTF32);
                }
        }
}
#endif

