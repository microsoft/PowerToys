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

namespace Python.Runtime {

    //========================================================================
    // Implements the __overloads__ attribute of method objects. This object
    // supports the [] syntax to explicitly select an overload by signature.
    //========================================================================

    internal class OverloadMapper : ExtensionType {

        MethodObject m;
        IntPtr target;

        public OverloadMapper(MethodObject m, IntPtr target) : base() {
            Runtime.Incref(target);
            this.target = target;
            this.m = m;
        }
 
         //====================================================================
         // Implement explicit overload selection using subscript syntax ([]).
         //====================================================================
 
         public static IntPtr mp_subscript(IntPtr tp, IntPtr idx) {
             OverloadMapper self = (OverloadMapper)GetManagedObject(tp);

            // Note: if the type provides a non-generic method with N args
            // and a generic method that takes N params, then we always
            // prefer the non-generic version in doing overload selection.

            Type[] types = Runtime.PythonArgsToTypeArray(idx);
            if (types == null) {
                 return Exceptions.RaiseTypeError("type(s) expected");
            }

             MethodInfo mi = MethodBinder.MatchSignature(self.m.info, types);
             if (mi == null) {
                string e = "No match found for signature";
                return Exceptions.RaiseTypeError(e);
             }

             MethodBinding mb = new MethodBinding(self.m, self.target);
             mb.info = mi;
             Runtime.Incref(mb.pyHandle);
             return mb.pyHandle;
         }

        //====================================================================
        // OverloadMapper  __repr__ implementation.
        //====================================================================

        public static IntPtr tp_repr(IntPtr op) {
            OverloadMapper self = (OverloadMapper)GetManagedObject(op);
            IntPtr doc = self.m.GetDocString();
            Runtime.Incref(doc);
            return doc;
         }

        //====================================================================
        // OverloadMapper dealloc implementation.
        //====================================================================

        public static new void tp_dealloc(IntPtr ob) {
            OverloadMapper self = (OverloadMapper)GetManagedObject(ob);
            Runtime.Decref(self.target);
            ExtensionType.FinalizeObject(self);
        }

    }


}
