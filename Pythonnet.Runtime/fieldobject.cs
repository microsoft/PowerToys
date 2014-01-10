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

    //========================================================================
    // Implements a Python descriptor type that provides access to CLR fields.
    //========================================================================

    internal class FieldObject : ExtensionType {

        FieldInfo info;

        public FieldObject(FieldInfo info) : base() {
            this.info = info;
        }

        //====================================================================
        // Descriptor __get__ implementation. This method returns the 
        // value of the field on the given object. The returned value
        // is converted to an appropriately typed Python object.
        //====================================================================

        public static IntPtr tp_descr_get(IntPtr ds, IntPtr ob, IntPtr tp) {
            FieldObject self = (FieldObject)GetManagedObject(ds);
            Object result;

            if (self == null) {
                return IntPtr.Zero;
            }

            FieldInfo info = self.info;

            if ((ob == IntPtr.Zero) || (ob == Runtime.PyNone)) {
                if (!info.IsStatic) {
                    Exceptions.SetError(Exceptions.TypeError, 
                               "instance attribute must be accessed " + 
                               "through a class instance"
                               );
                    return IntPtr.Zero;
                }
                try {
                    result = info.GetValue(null);
                    return Converter.ToPython(result, info.FieldType);
                }
                catch(Exception e) {
                    Exceptions.SetError(Exceptions.TypeError, e.Message);
                    return IntPtr.Zero;
                }
            }

            try {
                CLRObject co = (CLRObject)GetManagedObject(ob);
                result = info.GetValue(co.inst);
                return Converter.ToPython(result, info.FieldType);
            }
            catch(Exception e) {
                Exceptions.SetError(Exceptions.TypeError, e.Message);
                return IntPtr.Zero;
            }
        }

        //====================================================================
        // Descriptor __set__ implementation. This method sets the value of
        // a field based on the given Python value. The Python value must be
        // convertible to the type of the field.
        //====================================================================

        public static new int tp_descr_set(IntPtr ds, IntPtr ob, IntPtr val) {
            FieldObject self = (FieldObject)GetManagedObject(ds);
            Object newval;

            if (self == null) {
                return -1;
            }

            if (val == IntPtr.Zero) {
                Exceptions.SetError(Exceptions.TypeError, 
                                    "cannot delete field"
                                    );
                return -1;
            }

            FieldInfo info = self.info;

            if (info.IsLiteral || info.IsInitOnly) {
                Exceptions.SetError(Exceptions.TypeError, 
                                    "field is read-only"
                                    );
                return -1;
            }

            bool is_static = info.IsStatic;

            if ((ob == IntPtr.Zero) || (ob == Runtime.PyNone)) {
                if (!is_static) {
                    Exceptions.SetError(Exceptions.TypeError, 
                               "instance attribute must be set " + 
                               "through a class instance"
                               );
                    return -1;
                }
            }

            if (!Converter.ToManaged(val, info.FieldType, out newval, 
                                      true)) {
                return -1;
            }

            try {
                if (!is_static) {
                    CLRObject co = (CLRObject)GetManagedObject(ob);
                    info.SetValue(co.inst, newval);
                }
                else {
                    info.SetValue(null, newval);
                }
                return 0;
            }
            catch(Exception e) {
                Exceptions.SetError(Exceptions.TypeError, e.Message);
                return -1;
            }
        }

        //====================================================================
        // Descriptor __repr__ implementation.
        //====================================================================

        public static IntPtr tp_repr(IntPtr ob) {
            FieldObject self = (FieldObject)GetManagedObject(ob);
            string s = String.Format("<field '{0}'>", self.info.Name);
            return Runtime.PyString_FromStringAndSize(s, s.Length);
        }

    }


}
