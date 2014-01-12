// ==========================================================================
// This software is subject to the provisions of the Zope Public License,
// Version 2.0 (ZPL).  A copy of the ZPL should accompany this distribution.
// THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
// WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
// FOR A PARTICULAR PURPOSE.
// ==========================================================================

using System;
using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Python.Runtime {


    internal class CLRObject : ManagedType {

        internal Object inst;

        internal CLRObject(Object ob, IntPtr tp) : base() {

            IntPtr py = Runtime.PyType_GenericAlloc(tp, 0);

              int flags = (int)Marshal.ReadIntPtr(tp, TypeOffset.tp_flags);
              if ((flags & TypeFlags.Subclass) != 0) {
                IntPtr dict = Marshal.ReadIntPtr(py, ObjectOffset.ob_dict);
                if (dict == IntPtr.Zero) {
                    dict = Runtime.PyDict_New();
                    Marshal.WriteIntPtr(py, ObjectOffset.ob_dict, dict);
                }
            }

            GCHandle gc = GCHandle.Alloc(this);
            Marshal.WriteIntPtr(py, ObjectOffset.magic(), (IntPtr)gc);
            this.tpHandle = tp;
            this.pyHandle = py;
            this.gcHandle = gc;
            inst = ob;
        }


        internal static CLRObject GetInstance(Object ob, IntPtr pyType) {
            return new CLRObject(ob, pyType);
        }


        internal static CLRObject GetInstance(Object ob) {
            ClassBase cc = ClassManager.GetClass(ob.GetType());
            return GetInstance(ob, cc.tpHandle);
        }


        internal static IntPtr GetInstHandle(Object ob, IntPtr pyType) {
            CLRObject co = GetInstance(ob, pyType);
            return co.pyHandle;
        }


        internal static IntPtr GetInstHandle(Object ob, Type type) {
            ClassBase cc = ClassManager.GetClass(type);
            CLRObject co = GetInstance(ob, cc.tpHandle);
            return co.pyHandle;
        }


        internal static IntPtr GetInstHandle(Object ob) {
            CLRObject co = GetInstance(ob);
            return co.pyHandle;
        }


    }


}

