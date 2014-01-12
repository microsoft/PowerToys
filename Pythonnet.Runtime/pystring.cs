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
    /// Represents a Python (ansi) string object. See the documentation at
    /// http://www.python.org/doc/current/api/stringObjects.html for details.
    /// 2011-01-29: ...Then why does the string constructor call PyUnicode_FromUnicode()???
    /// </summary>

    public class PyString : PySequence {

    /// <summary>
    /// PyString Constructor
    /// </summary>
    ///
    /// <remarks>
    /// Creates a new PyString from an existing object reference. Note 
    /// that the instance assumes ownership of the object reference. 
    /// The object reference is not checked for type-correctness. 
    /// </remarks>

    public PyString(IntPtr ptr) : base(ptr) {}


    /// <summary>
    /// PyString Constructor
    /// </summary>
    ///
    /// <remarks>
    /// Copy constructor - obtain a PyString from a generic PyObject. 
    /// An ArgumentException will be thrown if the given object is not 
    /// a Python string object.
    /// </remarks>

    public PyString(PyObject o) : base() {
        if (!IsStringType(o)) {
            throw new ArgumentException("object is not a string");
        }
        Runtime.Incref(o.obj);
        obj = o.obj;
    }


    /// <summary>
    /// PyString Constructor
    /// </summary>
    ///
    /// <remarks>
    /// Creates a Python string from a managed string.
    /// </remarks>

    public PyString(string s) : base() {
        obj = Runtime.PyUnicode_FromUnicode(s, s.Length);
        if (obj == IntPtr.Zero) {
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

    public static bool IsStringType(PyObject value) {
        return Runtime.PyString_Check(value.obj);
    }

    }


}
