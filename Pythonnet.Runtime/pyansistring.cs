// ==========================================================================
// This is a user contribution to the pythondotnet project.
// THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
// WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
// FOR A PARTICULAR PURPOSE.
// ==========================================================================

using System;

namespace Python.Runtime {

    public class PyAnsiString : PySequence
    {
        /// <summary>
        /// PyAnsiString Constructor
        /// </summary>
        ///
        /// <remarks>
        /// Creates a new PyAnsiString from an existing object reference. Note
        /// that the instance assumes ownership of the object reference.
        /// The object reference is not checked for type-correctness.
        /// </remarks>

        public PyAnsiString(IntPtr ptr) : base(ptr) { }


        /// <summary>
        /// PyString Constructor
        /// </summary>
        ///
        /// <remarks>
        /// Copy constructor - obtain a PyAnsiString from a generic PyObject.
        /// An ArgumentException will be thrown if the given object is not
        /// a Python string object.
        /// </remarks>

        public PyAnsiString(PyObject o)
            : base()
        {
            if (!IsStringType(o))
            {
                throw new ArgumentException("object is not a string");
            }
            Runtime.Incref(o.obj);
            obj = o.obj;
        }


        /// <summary>
        /// PyAnsiString Constructor
        /// </summary>
        ///
        /// <remarks>
        /// Creates a Python string from a managed string.
        /// </remarks>

        public PyAnsiString(string s)
            : base()
        {
            obj = Runtime.PyString_FromStringAndSize(s, s.Length);
            if (obj == IntPtr.Zero)
            {
                throw new PythonException();
            }
        }


        /// <summary>
        /// IsStringType Method
        /// </summary>
        ///
        /// <remarks>
        /// Returns true if the given object is a Python string.
        /// </remarks>

        public static bool IsStringType(PyObject value)
        {
            return Runtime.PyString_Check(value.obj);
        }
    }
}