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
    /// Represents a Python long int object. See the documentation at
    /// http://www.python.org/doc/current/api/longObjects.html
    /// </summary>

    public class PyLong : PyNumber {

        /// <summary>
        /// PyLong Constructor
        /// </summary>
        ///
        /// <remarks>
        /// Creates a new PyLong from an existing object reference. Note 
        /// that the instance assumes ownership of the object reference. 
        /// The object reference is not checked for type-correctness. 
        /// </remarks>

        public PyLong(IntPtr ptr) : base(ptr) {}


        /// <summary>
        /// PyLong Constructor
        /// </summary>
        ///
        /// <remarks>
        /// Copy constructor - obtain a PyLong from a generic PyObject. An 
        /// ArgumentException will be thrown if the given object is not a
        /// Python long object.
        /// </remarks>

        public PyLong(PyObject o) : base() {
            if (!IsLongType(o)) {
                throw new ArgumentException("object is not a long");
            }
            Runtime.Incref(o.obj);
            obj = o.obj;
        }


        /// <summary>
        /// PyLong Constructor
        /// </summary>
        ///
        /// <remarks>
        /// Creates a new PyLong from an int32 value.
        /// </remarks>

        public PyLong(int value) : base() {
            obj = Runtime.PyLong_FromLong((long)value);
            if (obj == IntPtr.Zero) {
                throw new PythonException();
            }
        }


        /// <summary>
        /// PyLong Constructor
        /// </summary>
        ///
        /// <remarks>
        /// Creates a new PyLong from a uint32 value.
        /// </remarks>

        [CLSCompliant(false)]
        public PyLong(uint value) : base() {
            obj = Runtime.PyLong_FromLong((long)value);
            if (obj == IntPtr.Zero) {
                throw new PythonException();
            }
        }


        /// <summary>
        /// PyLong Constructor
        /// </summary>
        ///
        /// <remarks>
        /// Creates a new PyLong from an int64 value.
        /// </remarks>

        public PyLong(long value) : base() {
            obj = Runtime.PyLong_FromLongLong(value);
            if (obj == IntPtr.Zero) {
                throw new PythonException();
            }
        }


        /// <summary>
        /// PyLong Constructor
        /// </summary>
        ///
        /// <remarks>
        /// Creates a new PyLong from a uint64 value.
        /// </remarks>

        [CLSCompliant(false)]
        public PyLong(ulong value) : base() {
            obj = Runtime.PyLong_FromUnsignedLongLong(value);
            if (obj == IntPtr.Zero) {
                throw new PythonException();
            }
        }


        /// <summary>
        /// PyLong Constructor
        /// </summary>
        ///
        /// <remarks>
        /// Creates a new PyLong from an int16 value.
        /// </remarks>

        public PyLong(short value) : base() {
            obj = Runtime.PyLong_FromLong((long)value);
            if (obj == IntPtr.Zero) {
                throw new PythonException();
            }
        }


        /// <summary>
        /// PyLong Constructor
        /// </summary>
        ///
        /// <remarks>
        /// Creates a new PyLong from an uint16 value.
        /// </remarks>

        [CLSCompliant(false)]
        public PyLong(ushort value) : base() {
            obj = Runtime.PyLong_FromLong((long)value);
            if (obj == IntPtr.Zero) {
                throw new PythonException();
            }
        }


        /// <summary>
        /// PyLong Constructor
        /// </summary>
        ///
        /// <remarks>
        /// Creates a new PyLong from a byte value.
        /// </remarks>

        public PyLong(byte value) : base() {
            obj = Runtime.PyLong_FromLong((long)value);
            if (obj == IntPtr.Zero) {
                throw new PythonException();
            }
        }


        /// <summary>
        /// PyLong Constructor
        /// </summary>
        ///
        /// <remarks>
        /// Creates a new PyLong from an sbyte value.
        /// </remarks>

        [CLSCompliant(false)]
        public PyLong(sbyte value) : base() {
            obj = Runtime.PyLong_FromLong((long)value);
            if (obj == IntPtr.Zero) {
                throw new PythonException();
            }
        }


        /// <summary>
        /// PyLong Constructor
        /// </summary>
        ///
        /// <remarks>
        /// Creates a new PyLong from an double value.
        /// </remarks>

        public PyLong(double value) : base() {
            obj = Runtime.PyLong_FromDouble(value);
            if (obj == IntPtr.Zero) {
                throw new PythonException();
            }
        }


        /// <summary>
        /// PyLong Constructor
        /// </summary>
        ///
        /// <remarks>
        /// Creates a new PyLong from a string value.
        /// </remarks>

        public PyLong(string value) : base() {
            obj = Runtime.PyLong_FromString(value, IntPtr.Zero, 0);
            if (obj == IntPtr.Zero) {
                throw new PythonException();
            }
        }


        /// <summary>
        /// IsLongType Method
        /// </summary>
        ///
        /// <remarks>
        /// Returns true if the given object is a Python long.
        /// </remarks>

        public static bool IsLongType(PyObject value) {
            return Runtime.PyLong_Check(value.obj);
        }


        /// <summary>
        /// AsLong Method
        /// </summary>
        ///
        /// <remarks>
        /// <remarks>
        /// Convert a Python object to a Python long if possible, raising  
        /// a PythonException if the conversion is not possible. This is
        /// equivalent to the Python expression "long(object)".
        /// </remarks>

        public static PyLong AsLong(PyObject value) {
            IntPtr op = Runtime.PyNumber_Long(value.obj);
            if (op == IntPtr.Zero) {
                throw new PythonException();
            }
            return new PyLong(op);
        }

        /// <summary>
        /// ToInt16 Method
        /// </summary>
        ///
        /// <remarks>
        /// Return the value of the Python long object as an int16.
        /// </remarks>

        public short ToInt16()
        {
            return System.Convert.ToInt16(this.ToInt64());
        }


        /// <summary>
        /// ToInt32 Method
        /// </summary>
        ///
        /// <remarks>
        /// Return the value of the Python long object as an int32.
        /// </remarks>

        public int ToInt32()
        {
            return System.Convert.ToInt32(this.ToInt64());
        }


        /// <summary>
        /// ToInt64 Method
        /// </summary>
        ///
        /// <remarks>
        /// Return the value of the Python long object as an int64.
        /// </remarks>

        public long ToInt64()
        {
            return Runtime.PyLong_AsLongLong(obj);
        }
    }

}
