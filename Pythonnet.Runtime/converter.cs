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
using System.Runtime.InteropServices;
using System.Globalization;
using System.Security;

namespace Python.Runtime {

    //========================================================================
    // Performs data conversions between managed types and Python types.
    //========================================================================

    [SuppressUnmanagedCodeSecurityAttribute()]

    internal class Converter {

        private Converter() {}

        static NumberFormatInfo nfi;
        static Type objectType;
        static Type stringType;
        static Type doubleType;
        static Type int32Type;
        static Type int64Type;
        static Type flagsType;
        static Type boolType;
        //static Type typeType;

        static Converter () {
            nfi = NumberFormatInfo.InvariantInfo;
            objectType = typeof(Object);
            stringType = typeof(String);
            int32Type = typeof(Int32);
            int64Type = typeof(Int64);
            doubleType = typeof(Double);
            flagsType = typeof(FlagsAttribute);
            boolType = typeof(Boolean);
            //typeType = typeof(Type);
        }


        //====================================================================
        // Given a builtin Python type, return the corresponding CLR type.
        //====================================================================

        internal static Type GetTypeByAlias(IntPtr op) {
            if ((op == Runtime.PyStringType) ||
                (op == Runtime.PyUnicodeType)) {
                return stringType;
            }
            else if (op == Runtime.PyIntType) {
                return int32Type;
            }
            else if (op == Runtime.PyLongType) {
                return int64Type;
            }
            else if (op == Runtime.PyFloatType) {
                return doubleType;
            }
            else if (op == Runtime.PyBoolType) {
                return boolType;
            }
            return null;
        }


        //====================================================================
        // Return a Python object for the given native object, converting
        // basic types (string, int, etc.) into equivalent Python objects.
        // This always returns a new reference. Note that the System.Decimal
        // type has no Python equivalent and converts to a managed instance.
        //====================================================================

        internal static IntPtr ToPython(Object value, Type type) {
            IntPtr result = IntPtr.Zero;

            // Null always converts to None in Python.

            if (value == null) {
                result = Runtime.PyNone;
                Runtime.Incref(result);
                return result;
            }

            // hmm - from Python, we almost never care what the declared
            // type is. we'd rather have the object bound to the actual
            // implementing class.

            type = value.GetType();

            TypeCode tc = Type.GetTypeCode(type);

            switch(tc) {

            case TypeCode.Object:
                result = CLRObject.GetInstHandle(value, type);

                // XXX - hack to make sure we convert new-style class based
                // managed exception instances to wrappers ;(
                if (Runtime.wrap_exceptions) {
                    Exception e = value as Exception;
                    if (e != null) {
                        return Exceptions.GetExceptionInstanceWrapper(result);
                    }
                }

                return result;

            case TypeCode.String:
                return Runtime.PyUnicode_FromString((string)value);

            case TypeCode.Int32:
                return Runtime.PyInt_FromInt32((int)value);

            case TypeCode.Boolean:
                if ((bool)value) {
                    Runtime.Incref(Runtime.PyTrue);
                    return Runtime.PyTrue;
                }
                Runtime.Incref(Runtime.PyFalse);
                return Runtime.PyFalse;

            case TypeCode.Byte:
                return Runtime.PyInt_FromInt32((int)((byte)value));

            case TypeCode.Char:
                return Runtime.PyUnicode_FromOrdinal((int)((char)value));

            case TypeCode.Int16:
                return Runtime.PyInt_FromInt32((int)((short)value));

            case TypeCode.Int64:
                return Runtime.PyLong_FromLongLong((long)value);

            case TypeCode.Single:
                // return Runtime.PyFloat_FromDouble((double)((float)value));
                string ss = ((float)value).ToString(nfi);
                IntPtr ps = Runtime.PyString_FromString(ss);
                IntPtr op = Runtime.PyFloat_FromString(ps, IntPtr.Zero);
                Runtime.Decref(ps);
                return op;

            case TypeCode.Double:
                return Runtime.PyFloat_FromDouble((double)value);

            case TypeCode.SByte:
                return Runtime.PyInt_FromInt32((int)((sbyte)value));

            case TypeCode.UInt16:
                return Runtime.PyInt_FromInt32((int)((ushort)value));

            case TypeCode.UInt32:
                return Runtime.PyLong_FromUnsignedLong((uint)value);

            case TypeCode.UInt64:
                return Runtime.PyLong_FromUnsignedLongLong((ulong)value);

            default:
                result = CLRObject.GetInstHandle(value, type);
                return result;
            }

        }


        //====================================================================
        // In a few situations, we don't have any advisory type information
        // when we want to convert an object to Python.
        //====================================================================

        internal static IntPtr ToPythonImplicit(Object value) {
            if (value == null) {
                IntPtr result = Runtime.PyNone;
                Runtime.Incref(result);
                return result;
            }

            return ToPython(value, objectType);
        }


        //====================================================================
        // Return a managed object for the given Python object, taking funny
        // byref types into account.
        //====================================================================

        internal static bool ToManaged(IntPtr value, Type type,
                                       out object result, bool setError) {
            if (type.IsByRef) {
                type = type.GetElementType();
            }
            return Converter.ToManagedValue(value, type, out result, setError);
        }


        internal static bool ToManagedValue(IntPtr value, Type obType,
                                      out Object result, bool setError) {
            // Common case: if the Python value is a wrapped managed object
            // instance, just return the wrapped object.
            ManagedType mt = ManagedType.GetManagedObject(value);
            result = null;

            // XXX - hack to support objects wrapped in old-style classes
            // (such as exception objects).
            if (Runtime.wrap_exceptions) {
            if (mt == null) {
                if (Runtime.PyObject_IsInstance(
                            value, Exceptions.Exception
                            ) > 0) {
                    IntPtr p = Runtime.PyObject_GetAttrString(value, "_inner");
                    if (p != IntPtr.Zero) {
                        // This is safe because we know that the __dict__ of
                        // value holds a reference to _inner.
                        value = p;
                        Runtime.Decref(p);
                        mt = ManagedType.GetManagedObject(value);
                    }
                }
                IntPtr c = Exceptions.UnwrapExceptionClass(value);
                if ((c != IntPtr.Zero) && (c != value)) {
                    value = c;
                    Runtime.Decref(c);
                    mt = ManagedType.GetManagedObject(value);
                }
            }
            }

            if (mt != null) {
                if (mt is CLRObject) {
                    object tmp = ((CLRObject)mt).inst;
                    if (obType.IsInstanceOfType(tmp)) {
                        result = tmp;
                        return true;
                    }
                    string err = "value cannot be converted to {0}";
                    err = String.Format(err, obType);
                    Exceptions.SetError(Exceptions.TypeError, err);
                    return false;
                }
                if (mt is ClassBase) {
                    result = ((ClassBase)mt).type;
                    return true;
                }
                // shouldnt happen
                return false;
            }

            if (value == Runtime.PyNone && !obType.IsValueType) {
                result = null;
                return true;
            }

            if (obType.IsArray) {
                return ToArray(value, obType, out result, setError);
            }

            if (obType.IsEnum) {
                return ToEnum(value, obType, out result, setError);
            }

            // Conversion to 'Object' is done based on some reasonable
            // default conversions (Python string -> managed string,
            // Python int -> Int32 etc.).

            if (obType == objectType) {
                if (Runtime.IsStringType(value)) {
                    return ToPrimitive(value, stringType, out result,
                                       setError);
                }

                else if (Runtime.PyBool_Check(value)) {
                    return ToPrimitive(value, boolType, out result, setError);
                }

                else if (Runtime.PyInt_Check(value)) {
                    return ToPrimitive(value, int32Type, out result, setError);
                }

                else if (Runtime.PyLong_Check(value)) {
                    return ToPrimitive(value, int64Type, out result, setError);
                }

                else if (Runtime.PyFloat_Check(value)) {
                    return ToPrimitive(value, doubleType, out result, setError);
                }

                else if (Runtime.PySequence_Check(value)) {
                    return ToArray(value, typeof(object[]), out result,
                                   setError);
                }

                if (setError) {
                    Exceptions.SetError(Exceptions.TypeError,
                                        "value cannot be converted to Object"
                                        );
                }

                return false;
            }

            return ToPrimitive(value, obType, out result, setError);

        }

        //====================================================================
        // Convert a Python value to an instance of a primitive managed type.
        //====================================================================

        static bool ToPrimitive(IntPtr value, Type obType, out Object result,
                                bool setError) {

            IntPtr overflow = Exceptions.OverflowError;
            TypeCode tc = Type.GetTypeCode(obType);
            result = null;
            IntPtr op;
            int ival;

            switch(tc) {

            case TypeCode.String:
                string st = Runtime.GetManagedString(value);
                if (st == null) {
                    goto type_error;
                }
                result = st;
                return true;

            case TypeCode.Int32:
                // Trickery to support 64-bit platforms.
                if (IntPtr.Size == 4) {
                    op = Runtime.PyNumber_Int(value);

                    // As of Python 2.3, large ints magically convert :(
                    if (Runtime.PyLong_Check(op) ) {
                        Runtime.Decref(op);
                        goto overflow;
                    }

                    if (op == IntPtr.Zero) {
                        if (Exceptions.ExceptionMatches(overflow)) {
                            goto overflow;
                        }
                      goto type_error;
                    }
                    ival = (int)Runtime.PyInt_AsLong(op);
                    Runtime.Decref(op);
                    result = ival;
                    return true;
                }
                else {
                    op = Runtime.PyNumber_Long(value);
                    if (op == IntPtr.Zero) {
                        if (Exceptions.ExceptionMatches(overflow)) {
                            goto overflow;
                        }
                        goto type_error;
                    }
                    long ll = (long)Runtime.PyLong_AsLongLong(op);
                    Runtime.Decref(op);
                    if ((ll == -1) && Exceptions.ErrorOccurred()) {
                        goto overflow;
                    }
                    if (ll > Int32.MaxValue || ll < Int32.MinValue) {
                        goto overflow;
                    }
                    result = (int)ll;
                    return true;
                }

            case TypeCode.Boolean:
                result = (Runtime.PyObject_IsTrue(value) != 0);
                return true;

            case TypeCode.Byte:
                if (Runtime.PyObject_TypeCheck(value, Runtime.PyStringType)) {
                    if (Runtime.PyString_Size(value) == 1) {
                        op = Runtime.PyString_AS_STRING(value);
                        result = (byte)Marshal.ReadByte(op);
                        return true;
                    }
                    goto type_error;
                }

                op = Runtime.PyNumber_Int(value);
                if (op == IntPtr.Zero) {
                    if (Exceptions.ExceptionMatches(overflow)) {
                        goto overflow;
                    }
                    goto type_error;
                }
                ival = (int) Runtime.PyInt_AsLong(op);
                Runtime.Decref(op);

                if (ival > Byte.MaxValue || ival < Byte.MinValue) {
                    goto overflow;
                }
                byte b = (byte) ival;
                result = b;
                return true;

            case TypeCode.SByte:
                if (Runtime.PyObject_TypeCheck(value, Runtime.PyStringType)) {
                    if (Runtime.PyString_Size(value) == 1) {
                        op = Runtime.PyString_AS_STRING(value);
                        result = (sbyte)Marshal.ReadByte(op);
                        return true;
                    }
                    goto type_error;
                }

                op = Runtime.PyNumber_Int(value);
                if (op == IntPtr.Zero) {
                    if (Exceptions.ExceptionMatches(overflow)) {
                        goto overflow;
                    }
                    goto type_error;
                }
                ival = (int) Runtime.PyInt_AsLong(op);
                Runtime.Decref(op);

                if (ival > SByte.MaxValue || ival < SByte.MinValue) {
                    goto overflow;
                }
                sbyte sb = (sbyte) ival;
                result = sb;
                return true;

            case TypeCode.Char:

                if (Runtime.PyObject_TypeCheck(value, Runtime.PyStringType)) {
                    if (Runtime.PyString_Size(value) == 1) {
                        op = Runtime.PyString_AS_STRING(value);
                        result = (char)Marshal.ReadByte(op);
                        return true;
                    }
                    goto type_error;
                }

                else if (Runtime.PyObject_TypeCheck(value,
                                 Runtime.PyUnicodeType)) {
                    if (Runtime.PyUnicode_GetSize(value) == 1) {
                        op = Runtime.PyUnicode_AS_UNICODE(value);
#if (!UCS4)
                        // 2011-01-02: Marshal as character array because the cast
                        // result = (char)Marshal.ReadInt16(op); throws an OverflowException
                        // on negative numbers with Check Overflow option set on the project
                        Char[] buff = new Char[1];
                        Marshal.Copy(op, buff, 0, 1);
                        result = buff[0];
#else
                        // XXX this is probably NOT correct?
                        result = (char)Marshal.ReadInt32(op);
#endif
                        return true;
                    }
                    goto type_error;
                }

                op = Runtime.PyNumber_Int(value);
                if (op == IntPtr.Zero) {
                    goto type_error;
                }
                ival = Runtime.PyInt_AsLong(op);
                if (ival > Char.MaxValue || ival < Char.MinValue) {
                    goto overflow;
                }
                Runtime.Decref(op);
                result = (char)ival;
                return true;

            case TypeCode.Int16:
                op = Runtime.PyNumber_Int(value);
                if (op == IntPtr.Zero) {
                    if (Exceptions.ExceptionMatches(overflow)) {
                        goto overflow;
                    }
                    goto type_error;
                }
                ival = (int) Runtime.PyInt_AsLong(op);
                Runtime.Decref(op);
                if (ival > Int16.MaxValue || ival < Int16.MinValue) {
                    goto overflow;
                }
                short s = (short) ival;
                result = s;
                return true;

            case TypeCode.Int64:
                op = Runtime.PyNumber_Long(value);
                if (op == IntPtr.Zero) {
                    if (Exceptions.ExceptionMatches(overflow)) {
                        goto overflow;
                    }
                    goto type_error;
                }
                long l = (long)Runtime.PyLong_AsLongLong(op);
                Runtime.Decref(op);
                if ((l == -1) && Exceptions.ErrorOccurred()) {
                    goto overflow;
                }
                result = l;
                return true;

            case TypeCode.UInt16:
                op = Runtime.PyNumber_Int(value);
                if (op == IntPtr.Zero) {
                    if (Exceptions.ExceptionMatches(overflow)) {
                        goto overflow;
                    }
                    goto type_error;
                }
                ival = (int) Runtime.PyInt_AsLong(op);
                Runtime.Decref(op);
                if (ival > UInt16.MaxValue || ival < UInt16.MinValue) {
                    goto overflow;
                }
                ushort us = (ushort) ival;
                result = us;
                return true;

            case TypeCode.UInt32:
                op = Runtime.PyNumber_Long(value);
                if (op == IntPtr.Zero) {
                    if (Exceptions.ExceptionMatches(overflow)) {
                        goto overflow;
                    }
                    goto type_error;
                }
                uint ui = (uint)Runtime.PyLong_AsUnsignedLong(op);
                Runtime.Decref(op);
                if (Exceptions.ErrorOccurred()) {
                    goto overflow;
                }
                result = ui;
                return true;

            case TypeCode.UInt64:
                op = Runtime.PyNumber_Long(value);
                if (op == IntPtr.Zero) {
                    if (Exceptions.ExceptionMatches(overflow)) {
                        goto overflow;
                    }
                    goto type_error;
                }
                ulong ul = (ulong)Runtime.PyLong_AsUnsignedLongLong(op);
                Runtime.Decref(op);
                if (Exceptions.ErrorOccurred()) {
                    goto overflow;
                }
                result = ul;
                return true;


            case TypeCode.Single:
                op = Runtime.PyNumber_Float(value);
                if (op == IntPtr.Zero) {
                    if (Exceptions.ExceptionMatches(overflow)) {
                        goto overflow;
                    }
                    goto type_error;
                }
                double dd = Runtime.PyFloat_AsDouble(value);
                if (dd > Single.MaxValue || dd < Single.MinValue) {
                    goto overflow;
                }
                result = (float)dd;
                return true;

            case TypeCode.Double:
                op = Runtime.PyNumber_Float(value);
                if (op == IntPtr.Zero) {
                    goto type_error;
                }
                double d = Runtime.PyFloat_AsDouble(op);
                Runtime.Decref(op);
                if (d > Double.MaxValue || d < Double.MinValue) {
                    goto overflow;
                }
                result = d;
                return true;

            }


        type_error:

            if (setError) {
                string format = "'{0}' value cannot be converted to {1}";
                string tpName = Runtime.PyObject_GetTypeName(value);
                string error = String.Format(format, tpName, obType);
                Exceptions.SetError(Exceptions.TypeError, error);
            }

            return false;

        overflow:

            if (setError) {
                string error = "value too large to convert";
                Exceptions.SetError(Exceptions.OverflowError, error);
            }

            return false;

        }


        static void SetConversionError(IntPtr value, Type target) {
            IntPtr ob = Runtime.PyObject_Repr(value);
            string src = Runtime.GetManagedString(ob);
            Runtime.Decref(ob);
            string error = String.Format(
                           "Cannot convert {0} to {1}", src, target
                           );
            Exceptions.SetError(Exceptions.TypeError, error);
        }


        //====================================================================
        // Convert a Python value to a correctly typed managed array instance.
        // The Python value must support the Python sequence protocol and the
        // items in the sequence must be convertible to the target array type.
        //====================================================================

        static bool ToArray(IntPtr value, Type obType, out Object result,
                           bool setError) {

            Type elementType = obType.GetElementType();
            int size = Runtime.PySequence_Size(value);
            result = null;

            if (size < 0) {
                if (setError) {
                    SetConversionError(value, obType);
                }
                return false;
            }

            Array items = Array.CreateInstance(elementType, size);

            // XXX - is there a better way to unwrap this if it is a real
            // array?
            for (int i = 0; i < size; i++) {
                Object obj = null;
                IntPtr item = Runtime.PySequence_GetItem(value, i);
                if (item == IntPtr.Zero) {
                    if (setError) {
                        SetConversionError(value, obType);
                        return false;
                    }
                }

                if (!Converter.ToManaged(item, elementType, out obj, true)) {
                    Runtime.Decref(item);
                    return false;
                }

                items.SetValue(obj, i);
                Runtime.Decref(item);
            }

            result = items;
            return true;
        }


        //====================================================================
        // Convert a Python value to a correctly typed managed enum instance.
        //====================================================================

        static bool ToEnum(IntPtr value, Type obType, out Object result,
                           bool setError) {

            Type etype = Enum.GetUnderlyingType(obType);
            result = null;

            if (!ToPrimitive(value, etype, out result, setError)) {
                return false;
            }

            if (Enum.IsDefined(obType, result)) {
                result = Enum.ToObject(obType, result);
                return true;
            }

            if (obType.GetCustomAttributes(flagsType, true).Length > 0) {
                result = Enum.ToObject(obType, result);
                return true;
            }

            if (setError) {
                string error = "invalid enumeration value";
                Exceptions.SetError(Exceptions.ValueError, error);
            }

            return false;

        }



    }


}
