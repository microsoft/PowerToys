// ==========================================================================
// This software is subject to the provisions of the Zope Public License,
// Version 2.0 (ZPL).  A copy of the ZPL should accompany this distribution.
// THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
// WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
// FOR A PARTICULAR PURPOSE.
// ==========================================================================

using System;

namespace Python.Runtime {

    /// <summary>
    /// Represents a standard Python list object. See the documentation at
    /// http://www.python.org/doc/current/api/listObjects.html for details.
    /// </summary>

    public class PyList : PySequence {

        /// <summary>
        /// PyList Constructor
        /// </summary>
        ///
        /// <remarks>
        /// Creates a new PyList from an existing object reference. Note 
        /// that the instance assumes ownership of the object reference. 
        /// The object reference is not checked for type-correctness. 
        /// </remarks>

        public PyList(IntPtr ptr) : base(ptr) {}


        /// <summary>
        /// PyList Constructor
        /// </summary>
        ///
        /// <remarks>
        /// Copy constructor - obtain a PyList from a generic PyObject. An 
        /// ArgumentException will be thrown if the given object is not a
        /// Python list object.
        /// </remarks>

        public PyList(PyObject o) : base() {
            if (!IsListType(o)) {
                throw new ArgumentException("object is not a list");
            }
            Runtime.Incref(o.obj);
            obj = o.obj;
        }


        /// <summary>
        /// PyList Constructor
        /// </summary>
        ///
        /// <remarks>
        /// Creates a new empty Python list object.
        /// </remarks>

        public PyList() : base() {
            obj = Runtime.PyList_New(0);
            if (obj == IntPtr.Zero) {
                throw new PythonException();
            }
        }


        /// <summary>
        /// PyList Constructor
        /// </summary>
        ///
        /// <remarks>
        /// Creates a new Python list object from an array of PyObjects.
        /// </remarks>

        public PyList(PyObject[] items) : base() {
            int count = items.Length;
            obj = Runtime.PyList_New(count);
            for (int i = 0; i < count; i++) {
                IntPtr ptr = items[i].obj;
                Runtime.Incref(ptr);
                int r = Runtime.PyList_SetItem(obj, i, ptr);
                if (r < 0) {
                    throw new PythonException();
                }
            }
        }


        /// <summary>
        /// IsListType Method
        /// </summary>
        ///
        /// <remarks>
        /// Returns true if the given object is a Python list.
        /// </remarks>

        public static bool IsListType(PyObject value) {
            return Runtime.PyList_Check(value.obj);
        }


        /// <summary>
        /// AsList Method
        /// </summary>
        ///
        /// <remarks>
        /// Converts a Python object to a Python list if possible, raising  
        /// a PythonException if the conversion is not possible. This is
        /// equivalent to the Python expression "list(object)".
        /// </remarks>

        public static PyList AsList(PyObject value) {
            IntPtr op = Runtime.PySequence_List(value.obj);
            if (op == IntPtr.Zero) {
                throw new PythonException();
            }
            return new PyList(op);
        }


        /// <summary>
        /// Append Method
        /// </summary>
        ///
        /// <remarks>
        /// Append an item to the list object.
        /// </remarks>

        public void Append(PyObject item) {
            int r = Runtime.PyList_Append(obj, item.obj);
            if (r < 0) {
                throw new PythonException();
            }
        }

        /// <summary>
        /// Insert Method
        /// </summary>
        ///
        /// <remarks>
        /// Insert an item in the list object at the given index.
        /// </remarks>

        public void Insert(int index, PyObject item) {
            int r = Runtime.PyList_Insert(obj, index, item.obj);
            if (r < 0) {
                throw new PythonException();
            }
        }


        /// <summary>
        /// Reverse Method
        /// </summary>
        ///
        /// <remarks>
        /// Reverse the order of the list object in place.
        /// </remarks>

        public void Reverse() {
            int r = Runtime.PyList_Reverse(obj);
            if (r < 0) {
                throw new PythonException();
            }
        }


        /// <summary>
        /// Sort Method
        /// </summary>
        ///
        /// <remarks>
        /// Sort the list in place.
        /// </remarks>

        public void Sort() {
            int r = Runtime.PyList_Sort(obj);
            if (r < 0) {
                throw new PythonException();
            }
        }


    }


}
