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
    /// Represents a Python integer object. See the documentation at
    /// http://www.python.org/doc/current/api/intObjects.html for details.
    /// </summary>

    public class PyInt : PyNumber {

        /// <summary>
        /// PyInt Constructor
        /// </summary>
        ///
        /// <remarks>
        /// Creates a new PyInt from an existing object reference. Note 
        /// that the instance assumes ownership of the object reference.
        /// The object reference is not checked for type-correctness. 
        /// </remarks>

        public PyInt(IntPtr ptr) : base(ptr) {}


        /// <summary>
        /// PyInt Constructor
        /// </summary>
        ///
        /// <remarks>
        /// Copy constructor - obtain a PyInt from a generic PyObject. An 
        /// ArgumentException will be thrown if the given object is not a
        /// Python int object.
        /// </remarks>

        public PyInt(PyObject o) : base() {
            if (!IsIntType(o)) {
                throw new ArgumentException("object is not an int");
            }
            Runtime.Incref(o.obj);
            obj = o.obj;
        }


        /// <summary>
        /// PyInt Constructor
        /// </summary>
        ///
        /// <remarks>
        /// Creates a new Python int from an int32 value.
        /// </remarks>

        public PyInt(int value) : base() {
            obj = Runtime.PyInt_FromInt32(value);
            if (obj == IntPtr.Zero) {
                throw new PythonException();
            }
        }


        /// <summary>
        /// PyInt Constructor
        /// </summary>
        ///
        /// <remarks>
        /// Creates a new Python int from a uint32 value.
        /// </remarks>

        [CLSCompliant(false)]
        public PyInt(uint value) : base(IntPtr.Zero) {
            obj = Runtime.PyInt_FromInt64((long)value);
            if (obj == IntPtr.Zero) {
                throw new PythonException();
            }
        }


        /// <summary>
        /// PyInt Constructor
        /// </summary>
        ///
        /// <remarks>
        /// Creates a new Python int from an int64 value.
        /// </remarks>

        public PyInt(long value) : base(IntPtr.Zero) {
            obj = Runtime.PyInt_FromInt64(value);
            if (obj == IntPtr.Zero) {
                throw new PythonException();
            }
        }


        /// <summary>
        /// PyInt Constructor
        /// </summary>
        ///
        /// <remarks>
        /// Creates a new Python int from a uint64 value.
        /// </remarks>

        [CLSCompliant(false)]
        public PyInt(ulong value) : base(IntPtr.Zero) {
            obj = Runtime.PyInt_FromInt64((long)value);
            if (obj == IntPtr.Zero) {
                throw new PythonException();
            }
        }


        /// <summary>
        /// PyInt Constructor
        /// </summary>
        ///
        /// <remarks>
        /// Creates a new Python int from an int16 value.
        /// </remarks>

        public PyInt(short value) : this((int)value) {}


        /// <summary>
        /// PyInt Constructor
        /// </summary>
        ///
        /// <remarks>
        /// Creates a new Python int from a uint16 value.
        /// </remarks>

        [CLSCompliant(false)]
        public PyInt(ushort value) : this((int)value) {}


        /// <summary>
        /// PyInt Constructor
        /// </summary>
        ///
        /// <remarks>
        /// Creates a new Python int from a byte value.
        /// </remarks>

        public PyInt(byte value) : this((int)value) {}


        /// <summary>
        /// PyInt Constructor
        /// </summary>
        ///
        /// <remarks>
        /// Creates a new Python int from an sbyte value.
        /// </remarks>

        [CLSCompliant(false)]
        public PyInt(sbyte value) : this((int)value) {}


        /// <summary>
        /// PyInt Constructor
        /// </summary>
        ///
        /// <remarks>
        /// Creates a new Python int from a string value.
        /// </remarks>

        public PyInt(string value) : base() {
            obj = Runtime.PyInt_FromString(value, IntPtr.Zero, 0);
            if (obj == IntPtr.Zero) {
                throw new PythonException();
            }
        }


        /// <summary>
        /// IsIntType Method
        /// </summary>
        ///
        /// <remarks>
        /// Returns true if the given object is a Python int.
        /// </remarks>

        public static bool IsIntType(PyObject value) {
            return Runtime.PyInt_Check(value.obj);
        }


        /// <summary>
        /// AsInt Method
        /// </summary>
        ///
        /// <remarks>
        /// <remarks>
        /// Convert a Python object to a Python int if possible, raising  
        /// a PythonException if the conversion is not possible. This is
        /// equivalent to the Python expression "int(object)".
        /// </remarks>

        public static PyInt AsInt(PyObject value) {
            IntPtr op = Runtime.PyNumber_Int(value.obj);
            if (op == IntPtr.Zero) {
                throw new PythonException();
            }
            return new PyInt(op);
        }


        /// <summary>
        /// ToInt16 Method
        /// </summary>
        ///
        /// <remarks>
        /// Return the value of the Python int object as an int16.
        /// </remarks>

        public short ToInt16() {
            return System.Convert.ToInt16(this.ToInt32());
        }


        /// <summary>
        /// ToInt32 Method
        /// </summary>
        ///
        /// <remarks>
        /// Return the value of the Python int object as an int32.
        /// </remarks>

        public int ToInt32() {
            return Runtime.PyInt_AsLong(obj);
        }


        /// <summary>
        /// ToInt64 Method
        /// </summary>
        ///
        /// <remarks>
        /// Return the value of the Python int object as an int64.
        /// </remarks>

        public long ToInt64() {
            return System.Convert.ToInt64(this.ToInt32());
        }



    }

}
