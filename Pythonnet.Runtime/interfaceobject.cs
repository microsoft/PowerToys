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
    /// Provides the implementation for reflected interface types. Managed
    /// interfaces are represented in Python by actual Python type objects.
    /// Each of those type objects is associated with an instance of this
    /// class, which provides the implementation for the Python type.
    /// </summary>

    internal class InterfaceObject : ClassBase {

        internal ConstructorInfo ctor;

        internal InterfaceObject(Type tp) : base(tp) {
            CoClassAttribute coclass = (CoClassAttribute) 
              Attribute.GetCustomAttribute(tp, cc_attr);
            if (coclass != null) {
                ctor = coclass.CoClass.GetConstructor(Type.EmptyTypes);
            }
        }

        static Type cc_attr;

        static InterfaceObject() {
            cc_attr = typeof(CoClassAttribute);
        }

        //====================================================================
        // Implements __new__ for reflected interface types.
        //====================================================================

        public static IntPtr tp_new(IntPtr tp, IntPtr args, IntPtr kw) {
            InterfaceObject self = (InterfaceObject)GetManagedObject(tp);
            int nargs = Runtime.PyTuple_Size(args);
            Type type = self.type;
            Object obj;

            if (nargs == 1) {
                IntPtr inst = Runtime.PyTuple_GetItem(args, 0);
                CLRObject co = GetManagedObject(inst) as CLRObject;

                if ((co == null) || (!type.IsInstanceOfType(co.inst))) {
                    string msg = "object does not implement " + type.Name;
                    Exceptions.SetError(Exceptions.TypeError, msg);
                    return IntPtr.Zero;
                }

                obj = co.inst;
            }

            else if ((nargs == 0) && (self.ctor != null)) {
                obj = self.ctor.Invoke(null);

                if (obj == null || !type.IsInstanceOfType(obj)) {
                    Exceptions.SetError(Exceptions.TypeError,
                               "CoClass default constructor failed" 
                               );
                    return IntPtr.Zero;
                }
            }

            else {
                Exceptions.SetError(Exceptions.TypeError,
                                    "interface takes exactly one argument"
                                    );
                return IntPtr.Zero;
            }

            return CLRObject.GetInstHandle(obj, self.pyHandle);
        }


    }        


}
