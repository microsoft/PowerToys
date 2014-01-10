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
    /// Implements a Python type that wraps a CLR ctor call. Constructor objects
    /// support a .Overloads[] syntax to allow explicit ctor overload selection.
    /// </summary>
    /// <remarks>
    /// ClassManager stores a ConstructorBinding instance in the class's __dict__['Overloads']
    /// 
    /// SomeType.Overloads[Type, ...] works like this:
    /// 1) Python retreives the Overloads attribute from this ClassObject's dictionary normally
    /// and finds a non-null tp_descr_get slot which is called by the interpreter
    /// and returns an IncRef()ed pyHandle to itself.
    /// 2) The ConstructorBinding object handles the [] syntax in its mp_subscript by matching
    /// the Type object parameters to a contructor overload using Type.GetConstructor()
    ///  [NOTE: I don't know why method overloads are not searched the same way.]
    /// and creating the BoundContructor oject which contains ContructorInfo object.
    /// 3) In tp_call, if ctorInfo is not null, ctorBinder.InvokeRaw() is called.
    /// </remarks>
    internal class ConstructorBinding : ExtensionType
    {
        Type type;  // The managed Type being wrapped in a ClassObject
        IntPtr pyTypeHndl;  // The python type tells GetInstHandle which Type to create.
        ConstructorBinder ctorBinder;
        IntPtr repr;

        public ConstructorBinding(Type type, IntPtr pyTypeHndl, ConstructorBinder ctorBinder) : base() {
            this.type = type;
            Runtime.Incref(pyTypeHndl);
            this.pyTypeHndl = pyTypeHndl;
            this.ctorBinder = ctorBinder;
            repr = IntPtr.Zero;
        }

        /// <summary>
        /// Descriptor __get__ implementation.
        /// Implements a Python type that wraps a CLR ctor call that requires the use
        /// of a .Overloads[pyTypeOrType...] syntax to allow explicit ctor overload
        /// selection.
        /// </summary>
        /// <param name="op"> PyObject* to a Constructors wrapper </param>
        /// <param name="instance"> the instance that the attribute was accessed through,
        /// or None when the attribute is accessed through the owner </param>
        /// <param name="owner"> always the owner class </param>
        /// <returns> a CtorMapper (that borrows a reference to this python type and the
        /// ClassObject's ConstructorBinder) wrapper. </returns>
        /// 
        /// <remarks>
        /// Python 2.6.5 docs:
        /// object.__get__(self, instance, owner)
        /// Called to get the attribute of the owner class (class attribute access)
        /// or of an instance of that class (instance attribute access). 
        /// owner is always the owner class, while instance is the instance that
        /// the attribute was accessed through, or None when the attribute is accessed through the owner.
        /// This method should return the (computed) attribute value or raise an AttributeError exception.
        /// </remarks>
        public static IntPtr tp_descr_get(IntPtr op, IntPtr instance, IntPtr owner)
        {
            ConstructorBinding self = (ConstructorBinding)GetManagedObject(op);
            if (self == null) {
                return IntPtr.Zero;
                }

            // It doesn't seem to matter if it's accessed through an instance (rather than via the type).
            /*if (instance != IntPtr.Zero) {
            // This is ugly! PyObject_IsInstance() returns 1 for true, 0 for false, -1 for error...
                if (Runtime.PyObject_IsInstance(instance, owner) < 1) {
                    return Exceptions.RaiseTypeError("How in the world could that happen!");
                }
            }*/
            Runtime.Incref(self.pyHandle);  // Decref'd by the interpreter.
            return self.pyHandle;
        }

        //====================================================================
        // Implement explicit overload selection using subscript syntax ([]).
        //====================================================================
        /// <summary>
        /// ConstructorBinding.GetItem(PyObject *o, PyObject *key)
        /// Return element of o corresponding to the object key or NULL on failure.
        /// This is the equivalent of the Python expression o[key].
        /// </summary>
        /// <param name="tp"></param>
        /// <param name="idx"></param>
        /// <returns></returns>
        public static IntPtr mp_subscript(IntPtr op, IntPtr key) {
            ConstructorBinding self = (ConstructorBinding)GetManagedObject(op);

            Type[] types = Runtime.PythonArgsToTypeArray(key);
            if (types == null) {
                return Exceptions.RaiseTypeError("type(s) expected");
            }
            //MethodBase[] methBaseArray = self.ctorBinder.GetMethods();
            //MethodBase ci = MatchSignature(methBaseArray, types);
            ConstructorInfo ci = self.type.GetConstructor(types);
            if (ci == null) {
                string msg = "No match found for constructor signature";
                return Exceptions.RaiseTypeError(msg);
            }
            BoundContructor boundCtor = new BoundContructor(self.type, self.pyTypeHndl, self.ctorBinder, ci);

            /* Since nothing's chached, do we need the increment???
            Runtime.Incref(boundCtor.pyHandle);  // Decref'd by the interpreter??? */
            return boundCtor.pyHandle;
        }

        //====================================================================
        // ConstructorBinding  __repr__ implementation [borrowed from MethodObject].
        //====================================================================

        public static IntPtr tp_repr(IntPtr ob) {
            ConstructorBinding self = (ConstructorBinding)GetManagedObject(ob);
            if (self.repr != IntPtr.Zero) {
                Runtime.Incref(self.repr);
                return self.repr;
            }
            MethodBase[] methods = self.ctorBinder.GetMethods();
            string name = self.type.FullName;
            string doc = "";
            for (int i = 0; i < methods.Length; i++) {
                if (doc.Length > 0)
                    doc += "\n";
                string str = methods[i].ToString();
                int idx = str.IndexOf("(");
                doc += String.Format("{0}{1}", name, str.Substring(idx));
            }
            self.repr = Runtime.PyString_FromString(doc);
            Runtime.Incref(self.repr);
            return self.repr;
        }

        //====================================================================
        // ConstructorBinding dealloc implementation.
        //====================================================================

        public static new void tp_dealloc(IntPtr ob) {
            ConstructorBinding self = (ConstructorBinding)GetManagedObject(ob);
            Runtime.Decref(self.repr);
            Runtime.Decref(self.pyTypeHndl);
            ExtensionType.FinalizeObject(self);
        }
    }

    /// <summary>
    /// Implements a Python type that constucts the given Type given a particular ContructorInfo.
    /// </summary>
    /// <remarks>
    /// Here mostly because I wanted a new __repr__ function for the selected constructor.
    /// An earlier implementation hung the __call__ on the ContructorBinding class and
    /// returned an Incref()ed self.pyHandle from the __get__ function.
    /// </remarks>
    internal class BoundContructor : ExtensionType {
        Type type;  // The managed Type being wrapped in a ClassObject
        IntPtr pyTypeHndl;  // The python type tells GetInstHandle which Type to create.
        ConstructorBinder ctorBinder;
        ConstructorInfo ctorInfo;
        IntPtr repr;

        public BoundContructor(Type type, IntPtr pyTypeHndl, ConstructorBinder ctorBinder, ConstructorInfo ci)
            : base() {
            this.type = type;
            Runtime.Incref(pyTypeHndl);
            this.pyTypeHndl = pyTypeHndl;
            this.ctorBinder = ctorBinder;
            ctorInfo = ci;
            repr = IntPtr.Zero;
        }

        /// <summary>
        /// BoundContructor.__call__(PyObject *callable_object, PyObject *args, PyObject *kw)
        /// </summary>
        /// <param name="ob"> PyObject *callable_object </param>
        /// <param name="args"> PyObject *args </param>
        /// <param name="kw"> PyObject *kw </param>
        /// <returns> A reference to a new instance of the class by invoking the selected ctor(). </returns>
        public static IntPtr tp_call(IntPtr op, IntPtr args, IntPtr kw) {
            BoundContructor self = (BoundContructor)GetManagedObject(op);
            // Even though a call with null ctorInfo just produces the old behavior
            /*if (self.ctorInfo == null) {
                string msg = "Usage: Class.Overloads[CLR_or_python_Type, ...]";
                return Exceptions.RaiseTypeError(msg);
            }*/
            // Bind using ConstructorBinder.Bind and invoke the ctor providing a null instancePtr
            // which will fire self.ctorInfo using ConstructorInfo.Invoke().
            Object obj = self.ctorBinder.InvokeRaw(IntPtr.Zero, args, kw, self.ctorInfo);
            if (obj == null) {
                // XXX set an error
                return IntPtr.Zero;
            }
            // Instantiate the python object that wraps the result of the method call
            // and return the PyObject* to it.
            return CLRObject.GetInstHandle(obj, self.pyTypeHndl);
        }

        //====================================================================
        // BoundContructor  __repr__ implementation [borrowed from MethodObject].
        //====================================================================

        public static IntPtr tp_repr(IntPtr ob) {
            BoundContructor self = (BoundContructor)GetManagedObject(ob);
            if (self.repr != IntPtr.Zero) {
                Runtime.Incref(self.repr);
                return self.repr;
            }
            string name = self.type.FullName;
            string str = self.ctorInfo.ToString();
            int idx = str.IndexOf("(");
            str = String.Format("returns a new {0}{1}", name, str.Substring(idx));
            self.repr = Runtime.PyString_FromString(str);
            Runtime.Incref(self.repr);
            return self.repr;
        }

        //====================================================================
        // ConstructorBinding dealloc implementation.
        //====================================================================

        public static new void tp_dealloc(IntPtr ob) {
            BoundContructor self = (BoundContructor)GetManagedObject(ob);
            Runtime.Decref(self.repr);
            Runtime.Decref(self.pyTypeHndl);
            ExtensionType.FinalizeObject(self);
        }
    }
}
