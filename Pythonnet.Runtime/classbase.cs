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
using System.Security;
using System.Runtime.InteropServices;

namespace Python.Runtime {

    /// <summary>
    /// Base class for Python types that reflect managed types / classes.
    /// Concrete subclasses include ClassObject and DelegateObject. This
    /// class provides common attributes and common machinery for doing
    /// class initialization (initialization of the class __dict__). The
    /// concrete subclasses provide slot implementations appropriate for
    /// each variety of reflected type.
    /// </summary>

    internal class ClassBase : ManagedType {

        internal Indexer indexer;
        internal Type type;

        internal ClassBase(Type tp) : base() {
            indexer = null;
            type = tp;
        }

        internal virtual bool CanSubclass() {
            return (!this.type.IsEnum);
        }

        //====================================================================
        // Implements __init__ for reflected classes and value types.
        //====================================================================

        public static int tp_init(IntPtr ob, IntPtr args, IntPtr kw) {
            return 0;
        }

         //====================================================================
         // Default implementation of [] semantics for reflected types.
         //====================================================================
 
        public virtual IntPtr type_subscript(IntPtr idx) {
            return Exceptions.RaiseTypeError("unsubscriptable object");
        } 

        //====================================================================
        // Standard comparison implementation for instances of reflected types.
        //====================================================================

        public static int tp_compare(IntPtr ob, IntPtr other) {
            if (ob == other) {
                return 0;
            }

            CLRObject co1 = GetManagedObject(ob) as CLRObject;
            CLRObject co2 = GetManagedObject(other) as CLRObject;
            Object o1 = co1.inst;
            Object o2 = co2.inst;

            if (Object.Equals(o1, o2)) {
                return 0;
            }
            return -1;
        }


        //====================================================================
        // Standard iteration support for instances of reflected types. This
        // allows natural iteration over objects that either are IEnumerable
        // or themselves support IEnumerator directly.
        //====================================================================

        public static IntPtr tp_iter(IntPtr ob) {
            CLRObject co = GetManagedObject(ob) as CLRObject;
            if (co == null) {
                return Exceptions.RaiseTypeError("invalid object");
            }

            IEnumerable e = co.inst as IEnumerable;
            IEnumerator o;

            if (e != null) {
                o = e.GetEnumerator();
            }
            else {
                o = co.inst as IEnumerator;
                         
                if (o == null) {
                    string message = "iteration over non-sequence";
                    return Exceptions.RaiseTypeError(message);
                }
            }

            return new Iterator(o).pyHandle;
        }


        //====================================================================
        // Standard __hash__ implementation for instances of reflected types.
        //====================================================================

        public static IntPtr tp_hash(IntPtr ob) {
            CLRObject co = GetManagedObject(ob) as CLRObject;
            if (co == null) {
                return Exceptions.RaiseTypeError("unhashable type");
            }
            return new IntPtr(co.inst.GetHashCode());
        }


        //====================================================================
        // Standard __str__ implementation for instances of reflected types.
        //====================================================================

        public static IntPtr tp_str(IntPtr ob) {
            CLRObject co = GetManagedObject(ob) as CLRObject;
            if (co == null) {
                return Exceptions.RaiseTypeError("invalid object");
            }
            return Runtime.PyString_FromString(co.inst.ToString());
        }


        //====================================================================
        // Default implementations for required Python GC support.
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
        // Standard dealloc implementation for instances of reflected types.
        //====================================================================

        public static void tp_dealloc(IntPtr ob) {
            ManagedType self = GetManagedObject(ob);
            IntPtr dict = Marshal.ReadIntPtr(ob, ObjectOffset.ob_dict);
            if (dict != IntPtr.Zero) { 
                Runtime.Decref(dict);
            }
            Runtime.PyObject_GC_UnTrack(self.pyHandle);
            Runtime.PyObject_GC_Del(self.pyHandle);
            Runtime.Decref(self.tpHandle);
            self.gcHandle.Free();
        }


    }        

}
