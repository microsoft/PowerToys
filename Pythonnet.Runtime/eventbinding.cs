// ==========================================================================
// This software is subject to the provisions of the Zope Public License,
// Version 2.0 (ZPL).  A copy of the ZPL should accompany this distribution.
// THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
// WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
// FOR A PARTICULAR PURPOSE.
// ==========================================================================

using System;

namespace Python.Runtime {

    //========================================================================
    // Implements a Python event binding type, similar to a method binding.
    //========================================================================

    internal class EventBinding : ExtensionType {

        EventObject e;
        IntPtr target;

        public EventBinding(EventObject e, IntPtr target) : base() {
            Runtime.Incref(target);
            this.target = target;
            this.e = e;
        }


        //====================================================================
        // EventBinding += operator implementation.
        //====================================================================

        public static IntPtr nb_inplace_add(IntPtr ob, IntPtr arg) {
            EventBinding self = (EventBinding)GetManagedObject(ob);

            if (Runtime.PyCallable_Check(arg) < 1) {
                Exceptions.SetError(Exceptions.TypeError, 
                                    "event handlers must be callable"
                                    );
                return IntPtr.Zero;
            }

            if(!self.e.AddEventHandler(self.target, arg)) {
                return IntPtr.Zero;
            }

            Runtime.Incref(self.pyHandle);
            return self.pyHandle;
        }


        //====================================================================
        // EventBinding -= operator implementation.
        //====================================================================

        public static IntPtr nb_inplace_subtract(IntPtr ob, IntPtr arg) {
            EventBinding self = (EventBinding)GetManagedObject(ob);

            if (Runtime.PyCallable_Check(arg) < 1) {
                Exceptions.SetError(Exceptions.TypeError, 
                                    "invalid event handler"
                                    );
                return IntPtr.Zero;
            }

            if (!self.e.RemoveEventHandler(self.target, arg)) {
                return IntPtr.Zero;
            }

            Runtime.Incref(self.pyHandle);
            return self.pyHandle;
        }


        //====================================================================
        // EventBinding  __hash__ implementation.
        //====================================================================

        public static IntPtr tp_hash(IntPtr ob) {
            EventBinding self = (EventBinding)GetManagedObject(ob);
            long x = 0;
            long y = 0;

            if (self.target != IntPtr.Zero) {
                x = Runtime.PyObject_Hash(self.target).ToInt64();
                if (x == -1) {
                    return new IntPtr(-1);
                }
            }
 
            y = Runtime.PyObject_Hash(self.e.pyHandle).ToInt64();
            if (y == -1) {
                return new IntPtr(-1);
            }

            x ^= y;

            if (x == -1) {
                x = -1;
            }

            return new IntPtr(x);
        }


        //====================================================================
        // EventBinding __repr__ implementation.
        //====================================================================

        public static IntPtr tp_repr(IntPtr ob) {
            EventBinding self = (EventBinding)GetManagedObject(ob);
            string type = (self.target == IntPtr.Zero) ? "unbound" : "bound";
            string s = String.Format("<{0} event '{1}'>", type, self.e.name);
            return Runtime.PyString_FromString(s);
        }


        //====================================================================
        // EventBinding dealloc implementation.
        //====================================================================

        public static new void tp_dealloc(IntPtr ob) {
            EventBinding self = (EventBinding)GetManagedObject(ob);
            Runtime.Decref(self.target);
            ExtensionType.FinalizeObject(self);
        }

    }


}
