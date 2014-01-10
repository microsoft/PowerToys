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
using System.Security.Permissions;

namespace Python.Runtime {

    //========================================================================
    // Implements a Python descriptor type that manages CLR properties.
    //========================================================================

    internal class PropertyObject : ExtensionType {

        PropertyInfo info;
        MethodInfo getter;
        MethodInfo setter;

        [StrongNameIdentityPermissionAttribute(SecurityAction.Assert)]
        public PropertyObject(PropertyInfo md) : base() {
            getter = md.GetGetMethod(true);
            setter = md.GetSetMethod(true);
            info = md;
        }


        //====================================================================
        // Descriptor __get__ implementation. This method returns the 
        // value of the property on the given object. The returned value
        // is converted to an appropriately typed Python object.
        //====================================================================

        public static IntPtr tp_descr_get(IntPtr ds, IntPtr ob, IntPtr tp) {
            PropertyObject self = (PropertyObject)GetManagedObject(ds);
            MethodInfo getter = self.getter;
            Object result;


            if (getter == null) {
                return Exceptions.RaiseTypeError("property cannot be read");
            }

            if ((ob == IntPtr.Zero) || (ob == Runtime.PyNone)) {
                if (!(getter.IsStatic)) {
                    Exceptions.SetError(Exceptions.TypeError, 
                               "instance property must be accessed through " + 
                               "a class instance"
                               );
                    return IntPtr.Zero;
                }

                try {
                    result = self.info.GetValue(null, null);
                    return Converter.ToPython(result, self.info.PropertyType);
                }
                catch(Exception e) {
                    return Exceptions.RaiseTypeError(e.Message);
                }
            }

            CLRObject co = GetManagedObject(ob) as CLRObject;
            if (co == null) {
                return Exceptions.RaiseTypeError("invalid target");
            }

            try {
                result = self.info.GetValue(co.inst, null);
                return Converter.ToPython(result, self.info.PropertyType);
            }
            catch(Exception e) {
                if (e.InnerException != null) {
                    e = e.InnerException;
                }
                Exceptions.SetError(e);
                return IntPtr.Zero;
            }
        }


        //====================================================================
        // Descriptor __set__ implementation. This method sets the value of
        // a property based on the given Python value. The Python value must 
        // be convertible to the type of the property.
        //====================================================================

        public static new int tp_descr_set(IntPtr ds, IntPtr ob, IntPtr val) {
            PropertyObject self = (PropertyObject)GetManagedObject(ds);
            MethodInfo setter = self.setter;
            Object newval;

            if (val == IntPtr.Zero) {
                Exceptions.RaiseTypeError("cannot delete property");
                return -1;
            }

            if (setter == null) {
                Exceptions.RaiseTypeError("property is read-only");
                return -1;
            }


            if (!Converter.ToManaged(val, self.info.PropertyType, out newval, 
                                      true)) {
                return -1;
            }

            bool is_static = setter.IsStatic;

            if ((ob == IntPtr.Zero) || (ob == Runtime.PyNone)) {
                if (!(is_static)) {
                    Exceptions.RaiseTypeError(
                    "instance property must be set on an instance"
                    );
                    return -1;
                }
            }

            try {
                if (!is_static) {
                    CLRObject co = GetManagedObject(ob) as CLRObject;
                    if (co == null) {
                        Exceptions.RaiseTypeError("invalid target");
                        return -1;
                    }
                    self.info.SetValue(co.inst, newval, null);
                }
                else {
                    self.info.SetValue(null, newval, null);                    
                }
                return 0;
            }
            catch(Exception e) {
                if (e.InnerException != null) {
                    e = e.InnerException;
                }
                Exceptions.SetError(e);
                return -1;
            }

        }


        //====================================================================
        // Descriptor __repr__ implementation.
        //====================================================================

        public static IntPtr tp_repr(IntPtr ob) {
            PropertyObject self = (PropertyObject)GetManagedObject(ob);
            string s = String.Format("<property '{0}'>", self.info.Name);
            return Runtime.PyString_FromStringAndSize(s, s.Length);
        }

    }


}
