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
    // Implements a Python binding type for CLR methods. These work much like
    // standard Python method bindings, but the same type is used to bind
    // both static and instance methods.
    //========================================================================

    internal class MethodBinding : ExtensionType {

        internal MethodInfo info;
        internal MethodObject m;
        internal IntPtr target;

        public MethodBinding(MethodObject m, IntPtr target) : base() {
            Runtime.Incref(target);
            this.target = target;
            this.info = null;
            this.m = m;
        }

         //====================================================================
         // Implement binding of generic methods using the subscript syntax [].
         //====================================================================
 
         public static IntPtr mp_subscript(IntPtr tp, IntPtr idx) {
             MethodBinding self = (MethodBinding)GetManagedObject(tp);

            Type[] types = Runtime.PythonArgsToTypeArray(idx);
            if (types == null) {
                 return Exceptions.RaiseTypeError("type(s) expected");
            }

             MethodInfo mi = MethodBinder.MatchParameters(self.m.info, types);
            if (mi == null) {
                string e = "No match found for given type params";
                return Exceptions.RaiseTypeError(e);
            }

             MethodBinding mb = new MethodBinding(self.m, self.target);
             mb.info = mi;
             Runtime.Incref(mb.pyHandle);
             return mb.pyHandle;            
         }


        //====================================================================
        // MethodBinding __getattribute__ implementation. 
        //====================================================================

        public static IntPtr tp_getattro(IntPtr ob, IntPtr key) {
            MethodBinding self = (MethodBinding)GetManagedObject(ob);

            if (!Runtime.PyString_Check(key)) {
                Exceptions.SetError(Exceptions.TypeError, "string expected");
                return IntPtr.Zero;
            }

            string name = Runtime.GetManagedString(key);
            if (name == "__doc__") {
                IntPtr doc = self.m.GetDocString();
                Runtime.Incref(doc);
                return doc;
            }

            // XXX deprecate __overloads__ soon...
            if (name == "__overloads__" || name == "Overloads") {
                OverloadMapper om = new OverloadMapper(self.m, self.target);
                Runtime.Incref(om.pyHandle);
                return om.pyHandle;            
            }

            return Runtime.PyObject_GenericGetAttr(ob, key);
        }


        //====================================================================
        // MethodBinding  __call__ implementation.
        //====================================================================

        public static IntPtr tp_call(IntPtr ob, IntPtr args, IntPtr kw) {
			MethodBinding self = (MethodBinding)GetManagedObject(ob);

			// This works around a situation where the wrong generic method is picked,
			// for example this method in the tests: string Overloaded<T>(int arg1, int arg2, string arg3)
			if (self.info != null)
			{
				if (self.info.IsGenericMethod)
				{
					int len = Runtime.PyTuple_Size(args);
					Type[] sigTp = Runtime.PythonArgsToTypeArray(args, true);
					if (sigTp != null)
					{
						Type[] genericTp = self.info.GetGenericArguments();
						MethodInfo betterMatch = MethodBinder.MatchSignatureAndParameters(self.m.info, genericTp, sigTp);
						if (betterMatch != null) self.info = betterMatch;
					}
				}
			}

			// This supports calling a method 'unbound', passing the instance
			// as the first argument. Note that this is not supported if any
			// of the overloads are static since we can't know if the intent
			// was to call the static method or the unbound instance method.

			if ((self.target == IntPtr.Zero) && (!self.m.IsStatic()))
			{
				int len = Runtime.PyTuple_Size(args);
				if (len < 1)
				{
					Exceptions.SetError(Exceptions.TypeError, "not enough arguments");
					return IntPtr.Zero;
				}
				IntPtr uargs = Runtime.PyTuple_GetSlice(args, 1, len);
				IntPtr inst = Runtime.PyTuple_GetItem(args, 0);
				Runtime.Incref(inst);
				IntPtr r = self.m.Invoke(inst, uargs, kw, self.info);
				Runtime.Decref(inst);
				Runtime.Decref(uargs);
				return r;
			}

			return self.m.Invoke(self.target, args, kw, self.info);
        }


        //====================================================================
        // MethodBinding  __hash__ implementation.
        //====================================================================

        public static IntPtr tp_hash(IntPtr ob) {
            MethodBinding self = (MethodBinding)GetManagedObject(ob);
            long x = 0;
            long y = 0;

            if (self.target != IntPtr.Zero) {
                x = Runtime.PyObject_Hash(self.target).ToInt64();
                if (x == -1) {
                    return new IntPtr(-1);
                }
            }
 
            y = Runtime.PyObject_Hash(self.m.pyHandle).ToInt64();
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
        // MethodBinding  __repr__ implementation.
        //====================================================================

        public static IntPtr tp_repr(IntPtr ob) {
            MethodBinding self = (MethodBinding)GetManagedObject(ob);
            string type = (self.target == IntPtr.Zero) ? "unbound" : "bound";
            string s = String.Format("<{0} method '{1}'>", type, self.m.name);
            return Runtime.PyString_FromStringAndSize(s, s.Length);
        }

        //====================================================================
        // MethodBinding dealloc implementation.
        //====================================================================

        public static new void tp_dealloc(IntPtr ob) {
            MethodBinding self = (MethodBinding)GetManagedObject(ob);
            Runtime.Decref(self.target);
            ExtensionType.FinalizeObject(self);
        }

    }


}
