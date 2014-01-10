// ==========================================================================
// This software is subject to the provisions of the Zope Public License,
// Version 2.0 (ZPL).  A copy of the ZPL should accompany this distribution.
// THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
// WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
// FOR A PARTICULAR PURPOSE.
// ==========================================================================

using System;
using System.Runtime.InteropServices;
using System.Collections;
using System.Reflection;

namespace Python.Runtime {

    /// <summary>
    /// Base class for extensions whose instances *share* a single Python
    /// type object, such as the types that represent CLR methods, fields, 
    /// etc. Instances implemented by this class do not support subtyping.
    /// </summary>

    internal abstract class ExtensionType : ManagedType {

        public ExtensionType() : base() {

            // Create a new PyObject whose type is a generated type that is
            // implemented by the particuar concrete ExtensionType subclass.
            // The Python instance object is related to an instance of a 
            // particular concrete subclass with a hidden CLR gchandle.

            IntPtr tp = TypeManager.GetTypeHandle(this.GetType());

//              int rc = (int)Marshal.ReadIntPtr(tp, TypeOffset.ob_refcnt);
//              if (rc > 1050) {
//              DebugUtil.Print("tp is: ", tp);
//              DebugUtil.DumpType(tp);
//              }

            IntPtr py = Runtime.PyType_GenericAlloc(tp, 0);

            GCHandle gc = GCHandle.Alloc(this);
            Marshal.WriteIntPtr(py, ObjectOffset.magic(), (IntPtr)gc);

            // We have to support gc because the type machinery makes it very
            // hard not to - but we really don't have a need for it in most
            // concrete extension types, so untrack the object to save calls
            // from Python into the managed runtime that are pure overhead.

            Runtime.PyObject_GC_UnTrack(py);

            this.tpHandle = tp;
            this.pyHandle = py;
            this.gcHandle = gc;
        }


        //====================================================================
        // Common finalization code to support custom tp_deallocs.
        //====================================================================

        public static void FinalizeObject(ManagedType self) {
            Runtime.PyObject_GC_Del(self.pyHandle);
            Runtime.Decref(self.tpHandle);
            self.gcHandle.Free();
        }


        //====================================================================
        // Type __setattr__ implementation.
        //====================================================================

        public static int tp_setattro(IntPtr ob, IntPtr key, IntPtr val) {
            string message = "type does not support setting attributes";
            if (val == IntPtr.Zero) {
                message = "readonly attribute";
            }
            Exceptions.SetError(Exceptions.TypeError, message);
            return -1;
        }


        //====================================================================
        // Default __set__ implementation - this prevents descriptor instances
        // being silently replaced in a type __dict__ by default __setattr__.
        //====================================================================

        public static int tp_descr_set(IntPtr ds, IntPtr ob, IntPtr val) {
            string message = "attribute is read-only";
            Exceptions.SetError(Exceptions.AttributeError, message);
            return -1;
        }


        //====================================================================
        // Required Python GC support.
        //====================================================================

        public static int tp_traverse(IntPtr ob, IntPtr func, IntPtr args) {
            return 0;
        }


        public static int tp_clear(IntPtr ob) {
            return 0;
        }


        public static int tp_is_gc(IntPtr type) {
            return 1;
        }


        //====================================================================
        // Default dealloc implementation.
        //====================================================================

        public static void tp_dealloc(IntPtr ob) {
            // Clean up a Python instance of this extension type. This 
            // frees the allocated Python object and decrefs the type.
            ManagedType self = GetManagedObject(ob);
            FinalizeObject(self);
        }


    }


}
