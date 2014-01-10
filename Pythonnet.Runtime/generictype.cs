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

    /// <summary>
    /// Implements reflected generic types. Note that the Python behavior
    /// is the same for both generic type definitions and constructed open
    /// generic types. Both are essentially factories for creating closed
    /// types based on the required generic type parameters.
    /// </summary>

    internal class GenericType : ClassBase {

        internal GenericType(Type tp) : base(tp) {}

        //====================================================================
        // Implements __new__ for reflected generic types.
        //====================================================================

        public static IntPtr tp_new(IntPtr tp, IntPtr args, IntPtr kw) {
            Exceptions.SetError(Exceptions.TypeError, 
                               "cannot instantiate an open generic type"
                               );
            return IntPtr.Zero;
        }


        //====================================================================
        // Implements __call__ for reflected generic types.
        //====================================================================

        public static IntPtr tp_call(IntPtr ob, IntPtr args, IntPtr kw) {
            Exceptions.SetError(Exceptions.TypeError, 
                                "object is not callable");
            return IntPtr.Zero;
        }

        //====================================================================
        // Implements subscript syntax for reflected generic types. A closed 
        // type is created by binding the generic type via subscript syntax:
        // inst = List[str]()
        //====================================================================

        public override IntPtr type_subscript(IntPtr idx) {
            Type[] types = Runtime.PythonArgsToTypeArray(idx);
            if (types == null) {
                return Exceptions.RaiseTypeError("type(s) expected");
            }
            if (!this.type.IsGenericTypeDefinition) {
                return Exceptions.RaiseTypeError(
                                  "type is not a generic type definition"
                                  );
            }

            // This is a little tricky, because an instance of GenericType
            // may represent either a specific generic type, or act as an
            // alias for one or more generic types with the same base name.

            if (this.type.ContainsGenericParameters) {
                Type[] args = this.type.GetGenericArguments();
                Type target = null;

                if (args.Length == types.Length) {
                    target = this.type;
                }
                else {
                    foreach (Type t in 
                             GenericUtil.GenericsForType(this.type)) {
                        if (t.GetGenericArguments().Length == types.Length) {
                            target = t;
                            break;
                        }
                    }
                }

                if (target != null) {
                    Type t = target.MakeGenericType(types);
                    ManagedType c = (ManagedType)ClassManager.GetClass(t);
                    Runtime.Incref(c.pyHandle);
                    return c.pyHandle;
                }
                return Exceptions.RaiseTypeError("no type matches params");
            }

            return Exceptions.RaiseTypeError("unsubscriptable object");
        }

    }

}
