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

namespace Python.Runtime {

    //========================================================================
    // Implements a Python type that represents a CLR method. Method objects
    // support a subscript syntax [] to allow explicit overload selection.
    //========================================================================
    // TODO: ForbidPythonThreadsAttribute per method info

    internal class MethodObject : ExtensionType {

        internal MethodInfo[] info;
        internal string name;
        internal MethodBinding unbound;
        internal MethodBinder binder;
        internal bool is_static = false;
        internal IntPtr doc;

        public MethodObject(string name, MethodInfo[] info) : base() {
            _MethodObject(name, info);
        }

        public MethodObject(string name, MethodInfo[] info, bool allow_threads) : base()
        {
            _MethodObject(name, info);
            binder.allow_threads = allow_threads;
        }

        private void _MethodObject(string name, MethodInfo[] info)
        {
            this.name = name;
            this.info = info;
            binder = new MethodBinder();
            for (int i = 0; i < info.Length; i++)
            {
                MethodInfo item = (MethodInfo)info[i];
                binder.AddMethod(item);
                if (item.IsStatic)
                {
                    this.is_static = true;
                }
            }
        }

        public virtual IntPtr Invoke(IntPtr inst, IntPtr args, IntPtr kw) {
            return this.Invoke(inst, args, kw, null);
        }
 
        public virtual IntPtr Invoke(IntPtr target, IntPtr args, IntPtr kw,
                                     MethodBase info) {
            return binder.Invoke(target, args, kw, info, this.info);
        }

        //====================================================================
        // Helper to get docstrings from reflected method / param info.
        //====================================================================

        internal IntPtr GetDocString() {
            if (doc != IntPtr.Zero) {
                return doc;
            }
            string str = "";
			Type marker = typeof(DocStringAttribute);
            MethodBase[] methods = binder.GetMethods();
            foreach (MethodBase method in methods) {
                if (str.Length > 0)
                    str += Environment.NewLine;
				Attribute[] attrs = (Attribute[]) method.GetCustomAttributes(marker, false);
	            if (attrs.Length == 0) {
		                str += method.ToString();
		            }
				else {
					DocStringAttribute attr = (DocStringAttribute)attrs[0];
					str += attr.DocString;
				}
			}
            doc = Runtime.PyString_FromString(str);
            return doc;
        }


        //====================================================================
        // This is a little tricky: a class can actually have a static method
        // and instance methods all with the same name. That makes it tough
        // to support calling a method 'unbound' (passing the instance as the
        // first argument), because in this case we can't know whether to call
        // the instance method unbound or call the static method. 
        //
        // The rule we is that if there are both instance and static methods
        // with the same name, then we always call the static method. So this
        // method returns true if any of the methods that are represented by 
        // the descriptor are static methods (called by MethodBinding).
        //====================================================================

        internal bool IsStatic() {
            return this.is_static;
        }

        //====================================================================
        // Descriptor __getattribute__ implementation. 
        //====================================================================

        public static IntPtr tp_getattro(IntPtr ob, IntPtr key) {
            MethodObject self = (MethodObject)GetManagedObject(ob);

            if (!Runtime.PyString_Check(key)) {
                return Exceptions.RaiseTypeError("string expected");
            }

            string name = Runtime.GetManagedString(key);
            if (name == "__doc__") {
                IntPtr doc = self.GetDocString();
                Runtime.Incref(doc);
                return doc;
            }

            return Runtime.PyObject_GenericGetAttr(ob, key);
        }

        //====================================================================
        // Descriptor __get__ implementation. Accessing a CLR method returns
        // a "bound" method similar to a Python bound method. 
        //====================================================================

        public static IntPtr tp_descr_get(IntPtr ds, IntPtr ob, IntPtr tp) {
            MethodObject self = (MethodObject)GetManagedObject(ds);
            MethodBinding binding;

            // If the method is accessed through its type (rather than via
            // an instance) we return an 'unbound' MethodBinding that will
            // cached for future accesses through the type.

            if (ob == IntPtr.Zero) {
                if (self.unbound == null) {
                    self.unbound = new MethodBinding(self, IntPtr.Zero);
                }
                binding = self.unbound;
                Runtime.Incref(binding.pyHandle);;
                return binding.pyHandle;
            }

            if (Runtime.PyObject_IsInstance(ob, tp) < 1) {
                return Exceptions.RaiseTypeError("invalid argument");
            }

            binding = new MethodBinding(self, ob);
            return binding.pyHandle;
        }

        //====================================================================
        // Descriptor __repr__ implementation.
        //====================================================================

        public static IntPtr tp_repr(IntPtr ob) {
            MethodObject self = (MethodObject)GetManagedObject(ob);
            string s = String.Format("<method '{0}'>", self.name);
            return Runtime.PyString_FromStringAndSize(s, s.Length);
        }

        //====================================================================
        // Descriptor dealloc implementation.
        //====================================================================

        public static new void tp_dealloc(IntPtr ob) {
            MethodObject self = (MethodObject)GetManagedObject(ob);
            Runtime.Decref(self.doc);
            if (self.unbound != null) {
                Runtime.Decref(self.unbound.pyHandle);
            }
            ExtensionType.FinalizeObject(self);
        }


    }


}
