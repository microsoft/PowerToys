// ==========================================================================
// This software is subject to the provisions of the Zope Public License,
// Version 2.0 (ZPL).  A copy of the ZPL should accompany this distribution.
// THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
// WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
// FOR A PARTICULAR PURPOSE.
// ==========================================================================

using System;
using System.Runtime.InteropServices;

namespace Python.Runtime {

    /// <summary>
    /// Represents a Python float object. See the documentation at
    /// http://www.python.org/doc/current/api/floatObjects.html
    /// </summary>

    public class PyFloat : PyNumber {

        /// <summary>
        /// PyFloat Constructor
        /// </summary>
        ///
        /// <remarks>
        /// Creates a new PyFloat from an existing object reference. Note 
        /// that the instance assumes ownership of the object reference. 
        /// The object reference is not checked for type-correctness. 
        /// </remarks>

        public PyFloat(IntPtr ptr) : base(ptr) {}


        /// <summary>
        /// PyFloat Constructor
        /// </summary>
        ///
        /// <remarks>
        /// Copy constructor - obtain a PyFloat from a generic PyObject. An 
        /// ArgumentException will be thrown if the given object is not a
        /// Python float object.
        /// </remarks>

        public PyFloat(PyObject o) : base() {
            if (!IsFloatType(o)) {
                throw new ArgumentException("object is not a float");
            }
            Runtime.Incref(o.obj);
            obj = o.obj;
        }


        /// <summary>
        /// PyFloat Constructor
        /// </summary>
        ///
        /// <remarks>
        /// Creates a new Python float from a double value.
        /// </remarks>

        public PyFloat(double value) : base() {
            obj = Runtime.PyFloat_FromDouble(value);
            if (obj == IntPtr.Zero) {
                throw new PythonException();
            }
        }


        /// <summary>
        /// PyFloat Constructor
        /// </summary>
        ///
        /// <remarks>
        /// Creates a new Python float from a string value.
        /// </remarks>

        public PyFloat(string value) : base() {
            PyString s = new PyString(value);
            obj = Runtime.PyFloat_FromString(s.obj, IntPtr.Zero);
            if (obj == IntPtr.Zero) {
                throw new PythonException();
            }
        }


        /// <summary>
        /// IsFloatType Method
        /// </summary>
        ///
        /// <remarks>
        /// Returns true if the given object is a Python float.
        /// </remarks>

        public static bool IsFloatType(PyObject value) {
            return Runtime.PyFloat_Check(value.obj);
        }


        /// <summary>
        /// AsFloat Method
        /// </summary>
        ///
        /// <remarks>
        /// <remarks>
        /// Convert a Python object to a Python float if possible, raising  
        /// a PythonException if the conversion is not possible. This is
        /// equivalent to the Python expression "float(object)".
        /// </remarks>

        public static PyFloat AsFloat(PyObject value) {
            IntPtr op = Runtime.PyNumber_Float(value.obj);
            if (op == IntPtr.Zero) {
                throw new PythonException();
            }
            return new PyFloat(op);
        }


    }

}
