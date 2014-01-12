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
    /// Represents a Python dictionary object. See the documentation at
    /// http://www.python.org/doc/current/api/dictObjects.html for details.
    /// </summary>

    public class PyDict : PyObject {

        /// <summary>
        /// PyDict Constructor
        /// </summary>
        ///
        /// <remarks>
        /// Creates a new PyDict from an existing object reference. Note 
        /// that the instance assumes ownership of the object reference. 
        /// The object reference is not checked for type-correctness. 
        /// </remarks>

        public PyDict(IntPtr ptr) : base(ptr) {}


        /// <summary>
        /// PyDict Constructor
        /// </summary>
        ///
        /// <remarks>
        /// Creates a new Python dictionary object.
        /// </remarks>

        public PyDict() : base() {
            obj = Runtime.PyDict_New();
            if (obj == IntPtr.Zero) {
                throw new PythonException();
            }
        }


        /// <summary>
        /// PyDict Constructor
        /// </summary>
        ///
        /// <remarks>
        /// Copy constructor - obtain a PyDict from a generic PyObject. An 
        /// ArgumentException will be thrown if the given object is not a
        /// Python dictionary object.
        /// </remarks>

        public PyDict(PyObject o) : base() {
            if (!IsDictType(o)) {
                throw new ArgumentException("object is not a dict");
            }
            Runtime.Incref(o.obj);
            obj = o.obj;
        }


        /// <summary>
        /// IsDictType Method
        /// </summary>
        ///
        /// <remarks>
        /// Returns true if the given object is a Python dictionary.
        /// </remarks>

        public static bool IsDictType(PyObject value) {
            return Runtime.PyDict_Check(value.obj);
        }


        /// <summary>
        /// HasKey Method
        /// </summary>
        ///
        /// <remarks>
        /// Returns true if the object key appears in the dictionary.
        /// </remarks>

        public bool HasKey(PyObject key) {
            return (Runtime.PyMapping_HasKey(obj, key.obj) != 0);
        }


        /// <summary>
        /// HasKey Method
        /// </summary>
        ///
        /// <remarks>
        /// Returns true if the string key appears in the dictionary.
        /// </remarks>

        public bool HasKey(string key) {
            return HasKey(new PyString(key));
        }


        /// <summary>
        /// Keys Method
        /// </summary>
        ///
        /// <remarks>
        /// Returns a sequence containing the keys of the dictionary.
        /// </remarks>

        public PyObject Keys() {
            IntPtr items = Runtime.PyDict_Keys(obj);
            if (items == IntPtr.Zero) {
                throw new PythonException();
            }
            return new PyObject(items);
        }


        /// <summary>
        /// Values Method
        /// </summary>
        ///
        /// <remarks>
        /// Returns a sequence containing the values of the dictionary.
        /// </remarks>

        public PyObject Values() {
            IntPtr items = Runtime.PyDict_Values(obj);
            if (items == IntPtr.Zero) {
                throw new PythonException();
            }
            return new PyObject(items);
        }


        /// <summary>
        /// Items Method
        /// </summary>
        ///
        /// <remarks>
        /// Returns a sequence containing the items of the dictionary.
        /// </remarks>

        public PyObject Items() {
            IntPtr items = Runtime.PyDict_Items(obj);
            if (items == IntPtr.Zero) {
                throw new PythonException();
            }
            return new PyObject(items);
        }


        /// <summary>
        /// Copy Method
        /// </summary>
        ///
        /// <remarks>
        /// Returns a copy of the dictionary.
        /// </remarks>

        public PyDict Copy() {
            IntPtr op = Runtime.PyDict_Copy(obj);
            if (op == IntPtr.Zero) {
                throw new PythonException();
            }
            return new PyDict(op);
        }


        /// <summary>
        /// Update Method
        /// </summary>
        ///
        /// <remarks>
        /// Update the dictionary from another dictionary.
        /// </remarks>

        public void Update(PyObject other) {
            int result = Runtime.PyDict_Update(obj, other.obj);
            if (result < 0) {
                throw new PythonException();
            }
        }


        /// <summary>
        /// Clear Method
        /// </summary>
        ///
        /// <remarks>
        /// Clears the dictionary.
        /// </remarks>

        public void Clear() {
            Runtime.PyDict_Clear(obj);
        }


    }

}
