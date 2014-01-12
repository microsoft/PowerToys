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
    /// Provides a managed interface to exceptions thrown by the Python 
    /// runtime.
    /// </summary>

    public class PythonException : System.Exception {

    private IntPtr _pyType = IntPtr.Zero;
    private IntPtr _pyValue = IntPtr.Zero;
    private IntPtr _pyTB = IntPtr.Zero;
    private string _tb = "";
    private string _message = "";
    private bool disposed = false;

    public PythonException() : base()
    {
        IntPtr gs = PythonEngine.AcquireLock();
        Runtime.PyErr_Fetch(ref _pyType, ref _pyValue, ref _pyTB);
        Runtime.Incref(_pyType);
        Runtime.Incref(_pyValue);
        Runtime.Incref(_pyTB);
        if ((_pyType != IntPtr.Zero) && (_pyValue != IntPtr.Zero))
        {
            string type = new PyObject(_pyType).GetAttr("__name__").ToString();
            string message = Runtime.GetManagedString(_pyValue);
            _message = type + " : " + message;
        }
        if (_pyTB != IntPtr.Zero)
        {
            PyObject tb_module = PythonEngine.ImportModule("traceback");
            _tb = tb_module.InvokeMethod("format_tb", new PyObject(_pyTB)).ToString();
        }
        PythonEngine.ReleaseLock(gs);
    }

    // Ensure that encapsulated Python objects are decref'ed appropriately
    // when the managed exception wrapper is garbage-collected.

    ~PythonException() {
        Dispose();
    }


    /// <summary>
    /// PyType Property
    /// </summary>
    ///
    /// <remarks>
    /// Returns the exception type as a Python object.
    /// </remarks>

    public IntPtr PyType 
    {
        get { return _pyType; }
    }

    /// <summary>
    /// PyValue Property
    /// </summary>
    ///
    /// <remarks>
    /// Returns the exception value as a Python object.
    /// </remarks>

    public IntPtr PyValue
    {
        get { return _pyValue; }
    }

    /// <summary>
    /// Message Property
    /// </summary>
    ///
    /// <remarks>
    /// A string representing the python exception message.
    /// </remarks>

    public override string Message
    {
        get { return _message; }
    }

    /// <summary>
    /// StackTrace Property
    /// </summary>
    ///
    /// <remarks>
    /// A string representing the python exception stack trace.
    /// </remarks>

    public override string StackTrace 
    {
        get { return _tb; }
    }


    /// <summary>
    /// Dispose Method
    /// </summary>
    ///
    /// <remarks>
    /// The Dispose method provides a way to explicitly release the 
    /// Python objects represented by a PythonException.
    /// </remarks>

    public void Dispose() {
        if (!disposed) {
        if (Runtime.Py_IsInitialized() > 0) {
            IntPtr gs = PythonEngine.AcquireLock();
            Runtime.Decref(_pyType);
            Runtime.Decref(_pyValue);
            // XXX Do we ever get TraceBack? //
            if (_pyTB != IntPtr.Zero) {
                Runtime.Decref(_pyTB);
            }
            PythonEngine.ReleaseLock(gs);
        }
        GC.SuppressFinalize(this);
        disposed = true;
        }
    }

    /// <summary>
    /// Matches Method
    /// </summary>
    ///
    /// <remarks>
    /// Returns true if the Python exception type represented by the 
    /// PythonException instance matches the given exception type.
    /// </remarks>

    public static bool Matches(IntPtr ob) {
        return Runtime.PyErr_ExceptionMatches(ob) != 0;
    }

    } 
}
