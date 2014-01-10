// ==========================================================================
// This software is subject to the provisions of the Zope Public License,
// Version 2.0 (ZPL).  A copy of the ZPL should accompany this distribution.
// THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
// WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
// FOR A PARTICULAR PURPOSE.
// ==========================================================================

using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Python.Runtime {

    /// <summary>
    /// Managed class that provides the implementation for reflected delegate
    /// types. Delegates are represented in Python by generated type objects. 
    /// Each of those type objects is associated an instance of this class, 
    /// which provides its implementation.
    /// </summary>

    internal class DelegateObject : ClassBase {

        MethodBinder binder;

        internal DelegateObject(Type tp) : base(tp) {
            binder = new MethodBinder(tp.GetMethod("Invoke"));
        }


        //====================================================================
        // Given a PyObject pointer to an instance of a delegate type, return
        // the true managed delegate the Python object represents (or null).
        //====================================================================

        private static Delegate GetTrueDelegate(IntPtr op) {
            CLRObject o = GetManagedObject(op) as CLRObject;
            if (o != null) {
                Delegate d = o.inst as Delegate;
                return d;
            }
            return null;
        }


        internal override bool CanSubclass() {
            return false;
        }


        //====================================================================
        // DelegateObject __new__ implementation. The result of this is a new
        // PyObject whose type is DelegateObject and whose ob_data is a handle
        // to an actual delegate instance. The method wrapped by the actual
        // delegate instance belongs to an object generated to relay the call
        // to the Python callable passed in.
        //====================================================================

        public static IntPtr tp_new(IntPtr tp, IntPtr args, IntPtr kw) {
            DelegateObject self = (DelegateObject)GetManagedObject(tp);

            if (Runtime.PyTuple_Size(args) != 1) {
                string message = "class takes exactly one argument";
                return Exceptions.RaiseTypeError(message);
            }

            IntPtr method = Runtime.PyTuple_GetItem(args, 0);

            if (Runtime.PyCallable_Check(method) != 1) {
                return Exceptions.RaiseTypeError("argument must be callable");
            }

            Delegate d = PythonEngine.DelegateManager.GetDelegate(self.type, method);
            return CLRObject.GetInstHandle(d, self.pyHandle);
        }



        //====================================================================
        // Implements __call__ for reflected delegate types.
        //====================================================================

        public static IntPtr tp_call(IntPtr ob, IntPtr args, IntPtr kw) {
            // todo: add fast type check!
            IntPtr pytype = Runtime.PyObject_TYPE(ob);
            DelegateObject self = (DelegateObject)GetManagedObject(pytype);
            CLRObject o = GetManagedObject(ob) as CLRObject;

            if (o == null) {
                return Exceptions.RaiseTypeError("invalid argument");
            }
            
            Delegate d = o.inst as Delegate;

            if (d == null) {
                return Exceptions.RaiseTypeError("invalid argument");
            }
            return self.binder.Invoke(ob, args, kw);
        }


        //====================================================================
        // Implements __cmp__ for reflected delegate types.
        //====================================================================

        public static new int tp_compare(IntPtr ob, IntPtr other) {
            Delegate d1 = GetTrueDelegate(ob);
            Delegate d2 = GetTrueDelegate(other);
            if (d1 == d2) {
                return 0;
            }
            return -1;
        }


    }        


}
