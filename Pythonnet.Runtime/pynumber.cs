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
    /// Represents a generic Python number. The methods of this class are 
    /// equivalent to the Python "abstract number API". See  
    /// http://www.python.org/doc/current/api/number.html for details.
    /// </summary>

    public class PyNumber : PyObject {

        protected PyNumber(IntPtr ptr) : base(ptr) {}

        protected PyNumber() : base() {}


        /// <summary>
        /// IsNumberType Method
        /// </summary>
        ///
        /// <remarks>
        /// Returns true if the given object is a Python numeric type.
        /// </remarks>

        public static bool IsNumberType(PyObject value) {
            return Runtime.PyNumber_Check(value.obj);
        }


        // TODO: add all of the PyNumber_XXX methods.




    }

}
