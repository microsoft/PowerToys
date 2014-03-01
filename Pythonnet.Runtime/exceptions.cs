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
using System.Collections;
using System.Runtime.InteropServices;


namespace Python.Runtime {

    /// <summary>
    /// Base class for Python types that reflect managed exceptions based on
    /// System.Exception
    /// </summary>
    /// <remarks>
    /// The Python wrapper for managed exceptions LIES about its inheritance
    /// tree. Although the real System.Exception is a subclass of 
    /// System.Object the Python type for System.Exception does NOT claim that
    /// it subclasses System.Object. Instead TypeManager.CreateType() uses 
    /// Python's exception.Exception class as base class for System.Exception.
    /// </remarks>
    internal class ExceptionClassObject : ClassObject {

        internal ExceptionClassObject(Type tp) : base(tp) {
        }

#if (PYTHON25 || PYTHON26 || PYTHON27)
        internal static Exception ToException(IntPtr ob) {
            CLRObject co = GetManagedObject(ob) as CLRObject;
            if (co == null) {
                return null;
            }
            Exception e = co.inst as Exception;
            if (e == null) {
                return null;
            }
            return e;
        }

        //====================================================================
        // Exception __str__ implementation
        //====================================================================
        
        public new static IntPtr tp_str(IntPtr ob) {
            Exception e = ToException(ob);
            if (e == null) {
                return Exceptions.RaiseTypeError("invalid object");
            }

            string message = String.Empty;
            if (e.Message != String.Empty) {
                message = e.Message;
            }
            if ((e.StackTrace != null) && (e.StackTrace != String.Empty)) {
                message = message + "\n" + e.StackTrace;
            }
            return Runtime.PyUnicode_FromString(message);
        }

        //====================================================================
        // Exception __repr__ implementation.
        //====================================================================

        public static IntPtr tp_repr(IntPtr ob) {
            Exception e = ToException(ob);
            if (e == null) {
                return Exceptions.RaiseTypeError("invalid object");
            }
            string name = e.GetType().Name;
            string message;
            if (e.Message != String.Empty) {
                message = String.Format("{0}('{1}',)", name, e.Message);
            } else {
                message = String.Format("{0}()", name);
            }
            return Runtime.PyUnicode_FromString(message);
        }
        //====================================================================
        // Exceptions __getattribute__ implementation. 
        // handles Python's args and message attributes
        //====================================================================

        public static IntPtr tp_getattro(IntPtr ob, IntPtr key)
        {
            if (!Runtime.PyString_Check(key)) {
                Exceptions.SetError(Exceptions.TypeError, "string expected");
                return IntPtr.Zero;
            }

            string name = Runtime.GetManagedString(key);
            if (name == "args") {
                Exception e = ToException(ob);
                IntPtr args;
                if (e.Message != String.Empty) {
                    args = Runtime.PyTuple_New(1);
                    IntPtr msg = Runtime.PyUnicode_FromString(e.Message);
                    Runtime.PyTuple_SetItem(args, 0, msg);
                } else {
                    args = Runtime.PyTuple_New(0);
                }
                return args;
            }

            if (name == "message") {
                return ExceptionClassObject.tp_str(ob);
            }

            return Runtime.PyObject_GenericGetAttr(ob, key);
        }
#endif      // (PYTHON25 || PYTHON26 || PYTHON27)
    }

    /// <summary>
    /// Encapsulates the Python exception APIs.
    /// </summary>
    /// <remarks>
    /// Readability of the Exceptions class improvements as we look toward version 2.7 ...
    /// </remarks>

    public class Exceptions {

        internal static IntPtr warnings_module;
        internal static IntPtr exceptions_module;

        private Exceptions() {}

        //===================================================================
        // Initialization performed on startup of the Python runtime.
        //===================================================================

        internal static void Initialize() {
            exceptions_module = Runtime.PyImport_ImportModule("exceptions");
            Exceptions.ErrorCheck(exceptions_module);
            warnings_module = Runtime.PyImport_ImportModule("warnings");
            Exceptions.ErrorCheck(warnings_module);
            Type type = typeof(Exceptions);
            foreach (FieldInfo fi in type.GetFields(BindingFlags.Public | 
                                                    BindingFlags.Static)) {
                IntPtr op = Runtime.PyObject_GetAttrString(exceptions_module, fi.Name);
                if (op != IntPtr.Zero) {
                    fi.SetValue(type, op);
                }
                else {
                    fi.SetValue(type, IntPtr.Zero);
                    DebugUtil.Print("Unknown exception: " + fi.Name);
                }
            }
            Runtime.PyErr_Clear();
            if (Runtime.wrap_exceptions) {
                SetupExceptionHack();
            }
        }


        //===================================================================
        // Cleanup resources upon shutdown of the Python runtime.
        //===================================================================

        internal static void Shutdown() {
            Type type = typeof(Exceptions);
            foreach (FieldInfo fi in type.GetFields(BindingFlags.Public | 
                                                    BindingFlags.Static)) {
                IntPtr op = (IntPtr)fi.GetValue(type);
                if (op != IntPtr.Zero) {
                      Runtime.Decref(op);
                }
            }
            Runtime.Decref(exceptions_module);
            Runtime.Decref(warnings_module);
        }

        /// <summary>
        ///  Shortcut for (pointer == NULL) -> throw PythonException
        /// </summary>
        /// <param name="pointer">Pointer to a Python object</param>
        internal unsafe static void ErrorCheck(IntPtr pointer) {
            if (pointer == IntPtr.Zero) {
                throw new PythonException();
            }
        }

        /// <summary>
        ///  Shortcut for (pointer == NULL or ErrorOccurred()) -> throw PythonException
        /// </summary>
        ///  Shortcut for (pointer == NULL) -> throw PythonException
        internal unsafe static void ErrorOccurredCheck(IntPtr pointer) {
            if ((pointer == IntPtr.Zero) || Exceptions.ErrorOccurred()) {
                throw new PythonException();
            }
        }

        // Versions of CPython up to 2.4 do not allow exceptions to be
        // new-style classes. To get around that restriction and provide
        // a consistent user experience for programmers, we wrap managed
        // exceptions in an old-style class that (through some dont-try-
        // this-at-home hackery) delegates to the managed exception and
        // obeys the conventions of both Python and managed exceptions.

        /// <remarks>
        /// Conditionally initialized variables!
        /// </remarks>
        static IntPtr ns_exc; // new-style class for System.Exception
        static IntPtr os_exc; // old-style class for System.Exception
        static Hashtable cache;

        /// <remarks>
        /// the lines
        /// // XXX - hack to raise a compatible old-style exception ;(
        /// if (Runtime.wrap_exceptions) {
        ///     CallOneOfTheseMethods();
        ///  
        /// </remarks>
        internal static void SetupExceptionHack() {
            ns_exc = ClassManager.GetClass(typeof(Exception)).pyHandle;
            cache = new Hashtable();

            string code = 
            "import exceptions\n" +
            "class Exception(exceptions.Exception):\n" +
            "    _class = None\n" +
            "    _inner = None\n" +
            "    \n" +
            "    #@property\n" +
            "    def message(self):\n" +
            "        return self.Message\n" +
            "    message = property(message)\n" +
            "    \n" +
            "    def __init__(self, *args, **kw):\n" +
            "        inst = self.__class__._class(*args, **kw)\n" +
            "        self.__dict__['_inner'] = inst\n" +
            "        exceptions.Exception.__init__(self, *args, **kw)\n" +
            "\n" +
            "    def __getattr__(self, name, _marker=[]):\n" +
            "        inner = self.__dict__['_inner']\n" +
            "        v = getattr(inner, name, _marker)\n" +
            "        if v is not _marker:\n" +
            "            return v\n" +
            "        v = self.__dict__.get(name, _marker)\n" +
            "        if v is not _marker:\n" +
            "            return v\n" +
            "        raise AttributeError(name)\n" +
            "\n" +
            "    def __setattr__(self, name, value):\n" +
            "        inner = self.__dict__['_inner']\n" +
            "        setattr(inner, name, value)\n" +
            "\n" +
            "    def __str__(self):\n" +
            "        inner = self.__dict__.get('_inner')\n" +
            "        msg = getattr(inner, 'Message', '')\n" +
            "        st = getattr(inner, 'StackTrace', '')\n" +
            "        st = st and '\\n' + st or ''\n" +
            "        return msg + st\n" +
            "    \n" + 
            "    def __repr__(self):\n" +
            "        inner = self.__dict__.get('_inner')\n" +
            "        msg = getattr(inner, 'Message', '')\n" +
            "        name = self.__class__.__name__\n" +
            "        return '%s(\\'%s\\',)' % (name, msg) \n" +
            "\n";

            IntPtr dict = Runtime.PyDict_New();

            IntPtr builtins = Runtime.PyEval_GetBuiltins();
            Runtime.PyDict_SetItemString(dict, "__builtins__", builtins);

            IntPtr namestr = Runtime.PyString_FromString("System");
            Runtime.PyDict_SetItemString(dict, "__name__", namestr);
            Runtime.Decref(namestr);

            Runtime.PyDict_SetItemString(dict, "__file__", Runtime.PyNone);
            Runtime.PyDict_SetItemString(dict, "__doc__", Runtime.PyNone);

            IntPtr flag = Runtime.Py_file_input;
            IntPtr result = Runtime.PyRun_String(code, flag, dict, dict);
            Exceptions.ErrorCheck(result);
            Runtime.Decref(result);

            os_exc = Runtime.PyDict_GetItemString(dict, "Exception");
            Runtime.PyObject_SetAttrString(os_exc, "_class", ns_exc);
            Runtime.PyErr_Clear();
        }


        internal static IntPtr GenerateExceptionClass(IntPtr real) {
            if (real == ns_exc) {
                return os_exc;
            }

            IntPtr nbases = Runtime.PyObject_GetAttrString(real, "__bases__");
            if (Runtime.PyTuple_Size(nbases) != 1) {
                throw new SystemException("Invalid __bases__");
            }
            IntPtr nsbase = Runtime.PyTuple_GetItem(nbases, 0);
            Runtime.Decref(nbases);

            IntPtr osbase = GetExceptionClassWrapper(nsbase);
            IntPtr baselist = Runtime.PyTuple_New(1);
            Runtime.Incref(osbase);
            Runtime.PyTuple_SetItem(baselist, 0, osbase);
            IntPtr name = Runtime.PyObject_GetAttrString(real, "__name__");

            IntPtr dict = Runtime.PyDict_New();
            IntPtr mod = Runtime.PyObject_GetAttrString(real, "__module__");
            Runtime.PyDict_SetItemString(dict, "__module__", mod);
            Runtime.Decref(mod);

            IntPtr subc = Runtime.PyClass_New(baselist, dict, name);
            Runtime.Decref(baselist);
            Runtime.Decref(dict);
            Runtime.Decref(name);

            Runtime.PyObject_SetAttrString(subc, "_class", real);
            return subc;
        }

        internal static IntPtr GetExceptionClassWrapper(IntPtr real) {
            // Given the pointer to a new-style class representing a managed
            // exception, return an appropriate old-style class wrapper that
            // maintains all of the expectations and delegates to the wrapped
            // class.
            object ob = cache[real];
            if (ob == null) {
                IntPtr op = GenerateExceptionClass(real);
                cache[real] = op;
                return op;
            }
            return (IntPtr)ob;
        }

        internal static IntPtr GetExceptionInstanceWrapper(IntPtr real) {
            // Given the pointer to a new-style class instance representing a 
            // managed exception, return an appropriate old-style class 
            // wrapper instance that delegates to the wrapped instance.
            IntPtr tp = Runtime.PyObject_TYPE(real);
            if (Runtime.PyObject_TYPE(tp) == Runtime.PyInstanceType) {
                return real;
            }
            // Get / generate a class wrapper, instantiate it and set its
            // _inner attribute to the real new-style exception instance.
            IntPtr ct = GetExceptionClassWrapper(tp);
            Exceptions.ErrorCheck(ct);
            IntPtr op = Runtime.PyInstance_NewRaw(ct, IntPtr.Zero);
            Exceptions.ErrorCheck(op);
            IntPtr d = Runtime.PyObject_GetAttrString(op, "__dict__");
            Exceptions.ErrorCheck(d);
            Runtime.PyDict_SetItemString(d, "_inner", real);
            Runtime.Decref(d);
            return op;
        }

        internal static IntPtr UnwrapExceptionClass(IntPtr op) {
            // In some cases its necessary to recognize an exception *class*,
            // and obtain the inner (wrapped) exception class. This method
            // returns the inner class if found, or a null pointer.

            IntPtr d = Runtime.PyObject_GetAttrString(op, "__dict__");
            if (d == IntPtr.Zero) {
                Exceptions.Clear();
                return IntPtr.Zero;
            }
            IntPtr c = Runtime.PyDict_GetItemString(d, "_class");
            Runtime.Decref(d);
            if (c == IntPtr.Zero) {
                Exceptions.Clear();
            }
            return c;
        }

        /// <summary>
        /// GetException Method
        /// </summary>
        ///
        /// <remarks>
        /// Retrieve Python exception information as a PythonException
        /// instance. The properties of the PythonException may be used
        /// to access the exception type, value and traceback info.
        /// </remarks>

        public static PythonException GetException() {
            return null;
        }

        /// <summary>
        /// ExceptionMatches Method
        /// </summary>
        ///
        /// <remarks>
        /// Returns true if the current Python exception matches the given
        /// Python object. This is a wrapper for PyErr_ExceptionMatches.
        /// </remarks>

        public static bool ExceptionMatches(IntPtr ob) {
            return Runtime.PyErr_ExceptionMatches(ob) != 0;
        }

        /// <summary>
        /// ExceptionMatches Method
        /// </summary>
        ///
        /// <remarks>
        /// Returns true if the given Python exception matches the given
        /// Python object. This is a wrapper for PyErr_GivenExceptionMatches.
        /// </remarks>

        public static bool ExceptionMatches(IntPtr exc, IntPtr ob) {
            int i = Runtime.PyErr_GivenExceptionMatches(exc, ob);
            return (i != 0);
        }

        /// <summary>
        /// SetError Method
        /// </summary>
        ///
        /// <remarks>
        /// Sets the current Python exception given a native string.
        /// This is a wrapper for the Python PyErr_SetString call.
        /// </remarks>

        public static void SetError(IntPtr ob, string value) {
            Runtime.PyErr_SetString(ob, value);
        }

        /// <summary>
        /// SetError Method
        /// </summary>
        ///
        /// <remarks>
        /// Sets the current Python exception given a Python object.
        /// This is a wrapper for the Python PyErr_SetObject call.
        /// </remarks>

        public static void SetError(IntPtr ob, IntPtr value) {
            Runtime.PyErr_SetObject(ob, value);
        }

        /// <summary>
        /// SetError Method
        /// </summary>
        ///
        /// <remarks>
        /// Sets the current Python exception given a CLR exception
        /// object. The CLR exception instance is wrapped as a Python
        /// object, allowing it to be handled naturally from Python.
        /// </remarks>

        public static void SetError(Exception e) {

            // Because delegates allow arbitrary nestings of Python calling
            // managed calling Python calling... etc. it is possible that we
            // might get a managed exception raised that is a wrapper for a
            // Python exception. In that case we'd rather have the real thing.

            PythonException pe = e as PythonException;
            if (pe != null) {
                Runtime.PyErr_SetObject(pe.PyType, pe.PyValue);
                return;
            }

            IntPtr op = CLRObject.GetInstHandle(e);

            // XXX - hack to raise a compatible old-style exception ;(
            if (Runtime.wrap_exceptions) {
                op = GetExceptionInstanceWrapper(op);
            }
            IntPtr etype = Runtime.PyObject_GetAttrString(op, "__class__");
            Runtime.PyErr_SetObject(etype, op);
            Runtime.Decref(etype);
            Runtime.Decref(op);
        }

        /// <summary>
        /// ErrorOccurred Method
        /// </summary>
        ///
        /// <remarks>
        /// Returns true if an exception occurred in the Python runtime.
        /// This is a wrapper for the Python PyErr_Occurred call.
        /// </remarks>

        public static bool ErrorOccurred() {
            return Runtime.PyErr_Occurred() != 0;
        }

        /// <summary>
        /// Clear Method
        /// </summary>
        ///
        /// <remarks>
        /// Clear any exception that has been set in the Python runtime.
        /// </remarks>

        public static void Clear() {
            Runtime.PyErr_Clear();
        }

        //====================================================================
        // helper methods for raising warnings
        //====================================================================

        /// <summary>
        /// Alias for Python's warnings.warn() function.
        /// </summary>
        public static void warn(string message, IntPtr exception, int stacklevel)
        {
            if ((exception == IntPtr.Zero) ||
                (Runtime.PyObject_IsSubclass(exception, Exceptions.Warning) != 1)) {
                    Exceptions.RaiseTypeError("Invalid exception");
            }

            Runtime.Incref(warnings_module);
            IntPtr warn = Runtime.PyObject_GetAttrString(warnings_module, "warn");
            Runtime.Decref(warnings_module);
            Exceptions.ErrorCheck(warn);

            IntPtr args = Runtime.PyTuple_New(3);
            IntPtr msg = Runtime.PyString_FromString(message);
            Runtime.Incref(exception); // PyTuple_SetItem steals a reference
            IntPtr level = Runtime.PyInt_FromInt32(stacklevel);
            Runtime.PyTuple_SetItem(args, 0, msg);
            Runtime.PyTuple_SetItem(args, 1, exception);
            Runtime.PyTuple_SetItem(args, 2, level);

            IntPtr result = Runtime.PyObject_CallObject(warn, args);
            Exceptions.ErrorCheck(result);

            Runtime.Decref(warn);
            Runtime.Decref(result);
            Runtime.Decref(args);
        }

        public static void warn(string message, IntPtr exception)
        {
            warn(message, exception, 1);
        }

        public static void deprecation(string message, int stacklevel)
        {
            warn(message, Exceptions.DeprecationWarning, stacklevel);
        }

        public static void deprecation(string message)
        {
            deprecation(message, 1);
        }

        //====================================================================
        // Internal helper methods for common error handling scenarios.
        //====================================================================

        internal static IntPtr RaiseTypeError(string message) {
            Exceptions.SetError(Exceptions.TypeError, message);
            return IntPtr.Zero;
        }

        // 2010-11-16: Arranged in python (2.6 & 2.7) source header file order
        /* Predefined exceptions are
           puplic static variables on the Exceptions class filled in from
           the python class using reflection in Initialize() looked up by
		   name, not posistion. */
#if (PYTHON25 || PYTHON26 || PYTHON27)
        public static IntPtr BaseException;
#endif
        public static IntPtr Exception;
        public static IntPtr StopIteration;
#if (PYTHON25 || PYTHON26 || PYTHON27)
        public static IntPtr GeneratorExit;
#endif
        public static IntPtr StandardError;
        public static IntPtr ArithmeticError;
        public static IntPtr LookupError;
        
        public static IntPtr AssertionError;
        public static IntPtr AttributeError;
        public static IntPtr EOFError;
        public static IntPtr FloatingPointError;
        public static IntPtr EnvironmentError;
        public static IntPtr IOError;
        public static IntPtr OSError;
        public static IntPtr ImportError;
        public static IntPtr IndexError;
        public static IntPtr KeyError;
        public static IntPtr KeyboardInterrupt;
        public static IntPtr MemoryError;
        public static IntPtr NameError;
        public static IntPtr OverflowError;
        public static IntPtr RuntimeError;
        public static IntPtr NotImplementedError;
        public static IntPtr SyntaxError;
        public static IntPtr IndentationError;
        public static IntPtr TabError;
        public static IntPtr ReferenceError;
        public static IntPtr SystemError;
        public static IntPtr SystemExit;
        public static IntPtr TypeError;
        public static IntPtr UnboundLocalError;
        public static IntPtr UnicodeError;
        public static IntPtr UnicodeEncodeError;
        public static IntPtr UnicodeDecodeError;
        public static IntPtr UnicodeTranslateError;
        public static IntPtr ValueError;
        public static IntPtr ZeroDivisionError;
//#ifdef MS_WINDOWS
        //public static IntPtr WindowsError;
//#endif
//#ifdef __VMS
        //public static IntPtr VMSError;
//#endif

        //PyAPI_DATA(PyObject *) PyExc_BufferError;

        //PyAPI_DATA(PyObject *) PyExc_MemoryErrorInst;
        //PyAPI_DATA(PyObject *) PyExc_RecursionErrorInst;


        /* Predefined warning categories */
        public static IntPtr Warning;
        public static IntPtr UserWarning;
        public static IntPtr DeprecationWarning;
        public static IntPtr PendingDeprecationWarning;
        public static IntPtr SyntaxWarning;
        public static IntPtr RuntimeWarning;
        public static IntPtr FutureWarning;
#if (PYTHON25 || PYTHON26 || PYTHON27)
        public static IntPtr ImportWarning;
        public static IntPtr UnicodeWarning;
        //PyAPI_DATA(PyObject *) PyExc_BytesWarning;
#endif
    }


}
