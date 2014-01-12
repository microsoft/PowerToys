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
    /// Represents a generic Python object. The methods of this class are 
    /// generally equivalent to the Python "abstract object API". See  
    /// http://www.python.org/doc/current/api/object.html for details.
    /// </summary>

    public class PyObject : IDisposable {

    protected internal IntPtr obj = IntPtr.Zero;
    private bool disposed = false;

    /// <summary>
    /// PyObject Constructor
    /// </summary>
    ///
    /// <remarks>
    /// Creates a new PyObject from an IntPtr object reference. Note that
    /// the PyObject instance assumes ownership of the object reference 
    /// and the reference will be DECREFed when the PyObject is garbage 
    /// collected or explicitly disposed.
    /// </remarks>

    public PyObject(IntPtr ptr) {
        obj = ptr;
    }

    // Protected default constructor to allow subclasses to manage
    // initialization in different ways as appropriate.

    protected PyObject() {}

    // Ensure that encapsulated Python object is decref'ed appropriately
    // when the managed wrapper is garbage-collected.

    ~PyObject() {
        Dispose();
    }


    /// <summary>
    /// Handle Property
    /// </summary>
    ///
    /// <remarks>
    /// Gets the native handle of the underlying Python object. This
    /// value is generally for internal use by the PythonNet runtime. 
    /// </remarks>

    public IntPtr Handle {
        get { return obj; }
    }


    /// <summary>
    /// FromManagedObject Method
    /// </summary>
    ///
    /// <remarks>
    /// Given an arbitrary managed object, return a Python instance that
    /// reflects the managed object.
    /// </remarks>

    public static PyObject FromManagedObject(object ob) {
        // Special case: if ob is null, we return None.
        if (ob == null) {
            Runtime.Incref(Runtime.PyNone);
            return new PyObject(Runtime.PyNone);
        }
        IntPtr op = CLRObject.GetInstHandle(ob);
        return new PyObject(op);
    }


    /// <summary>
    /// AsManagedObject Method
    /// </summary>
    ///
    /// <remarks>
    /// Return a managed object of the given type, based on the 
    /// value of the Python object.
    /// </remarks>

    public object AsManagedObject(Type t) {
        Object result;
        if (!Converter.ToManaged(this.Handle, t, out result, false)) {
            throw new InvalidCastException("cannot convert object to target type");
        }
        return result;
    }


    /// <summary>
    /// Dispose Method
    /// </summary>
    ///
    /// <remarks>
    /// The Dispose method provides a way to explicitly release the 
    /// Python object represented by a PyObject instance. It is a good
    /// idea to call Dispose on PyObjects that wrap resources that are 
    /// limited or need strict lifetime control. Otherwise, references 
    /// to Python objects will not be released until a managed garbage 
    /// collection occurs.
    /// </remarks>

    public void Dispose() {
        if (!disposed) {
            if (Runtime.Py_IsInitialized() > 0) {
                IntPtr gs = PythonEngine.AcquireLock();
                Runtime.Decref(obj);
                obj = IntPtr.Zero;    
                PythonEngine.ReleaseLock(gs);
            }
            GC.SuppressFinalize(this);
            disposed = true;
        }
    }


    /// <summary>
    /// GetPythonType Method
    /// </summary>
    ///
    /// <remarks>
    /// Returns the Python type of the object. This method is equivalent
    /// to the Python expression: type(object).
    /// </remarks>

    public PyObject GetPythonType() {
        IntPtr tp = Runtime.PyObject_Type(obj);
        return new PyObject(tp);
    }


    /// <summary>
    /// TypeCheck Method
    /// </summary>
    ///
    /// <remarks>
    /// Returns true if the object o is of type typeOrClass or a subtype 
    /// of typeOrClass.
    /// </remarks>

    public bool TypeCheck(PyObject typeOrClass) {
        return Runtime.PyObject_TypeCheck(obj, typeOrClass.obj);
    }


    /// <summary>
    /// HasAttr Method
    /// </summary>
    ///
    /// <remarks>
    /// Returns true if the object has an attribute with the given name.
    /// </remarks>

    public bool HasAttr(string name) {
        return (Runtime.PyObject_HasAttrString(obj, name) != 0);
    }


    /// <summary>
    /// HasAttr Method
    /// </summary>
    ///
    /// <remarks>
    /// Returns true if the object has an attribute with the given name,
    /// where name is a PyObject wrapping a string or unicode object.
    /// </remarks>

    public bool HasAttr(PyObject name) {
        return (Runtime.PyObject_HasAttr(obj, name.obj) != 0);
    }


    /// <summary>
    /// GetAttr Method
    /// </summary>
    ///
    /// <remarks>
    /// Returns the named attribute of the Python object, or raises a 
    /// PythonException if the attribute access fails.
    /// </remarks>

    public PyObject GetAttr(string name) {
        IntPtr op = Runtime.PyObject_GetAttrString(obj, name);
        if (op == IntPtr.Zero) {
            throw new PythonException();
        }
        return new PyObject(op);
    }


    /// <summary>
    /// GetAttr Method
    /// </summary>
    ///
    /// <remarks>
    /// Returns the named attribute of the Python object, or the given
    /// default object if the attribute access fails.
    /// </remarks>

    public PyObject GetAttr(string name, PyObject _default) {
        IntPtr op = Runtime.PyObject_GetAttrString(obj, name);
        if (op == IntPtr.Zero) {
            Runtime.PyErr_Clear();
            return _default;
        }
        return new PyObject(op);
    }


    /// <summary>
    /// GetAttr Method
    /// </summary>
    ///
    /// <remarks>
    /// Returns the named attribute of the Python object or raises a 
    /// PythonException if the attribute access fails. The name argument 
    /// is a PyObject wrapping a Python string or unicode object.
    /// </remarks>

    public PyObject GetAttr(PyObject name) {
        IntPtr op = Runtime.PyObject_GetAttr(obj, name.obj);
        if (op == IntPtr.Zero) {
            throw new PythonException();
        }
        return new PyObject(op);
    }


    /// <summary>
    /// GetAttr Method
    /// </summary>
    ///
    /// <remarks>
    /// Returns the named attribute of the Python object, or the given
    /// default object if the attribute access fails. The name argument 
    /// is a PyObject wrapping a Python string or unicode object.
    /// </remarks>

    public PyObject GetAttr(PyObject name, PyObject _default) {
        IntPtr op = Runtime.PyObject_GetAttr(obj, name.obj);
        if (op == IntPtr.Zero) {
            Runtime.PyErr_Clear();
            return _default;
        }
        return new PyObject(op);
    }


    /// <summary>
    /// SetAttr Method
    /// </summary>
    ///
    /// <remarks>
    /// Set an attribute of the object with the given name and value. This
    /// method throws a PythonException if the attribute set fails.
    /// </remarks>

    public void SetAttr(string name, PyObject value) {
        int r = Runtime.PyObject_SetAttrString(obj, name, value.obj);
        if (r < 0) {
            throw new PythonException();
        }
    }


    /// <summary>
    /// SetAttr Method
    /// </summary>
    ///
    /// <remarks>
    /// Set an attribute of the object with the given name and value, 
    /// where the name is a Python string or unicode object. This method
    /// throws a PythonException if the attribute set fails.
    /// </remarks>

    public void SetAttr(PyObject name, PyObject value) {
        int r = Runtime.PyObject_SetAttr(obj, name.obj, value.obj);
        if (r < 0) {
            throw new PythonException();
        }
    }


    /// <summary>
    /// DelAttr Method
    /// </summary>
    ///
    /// <remarks>
    /// Delete the named attribute of the Python object. This method
    /// throws a PythonException if the attribute set fails.
    /// </remarks>

    public void DelAttr(string name) {
        int r = Runtime.PyObject_SetAttrString(obj, name, IntPtr.Zero);
        if (r < 0) {
            throw new PythonException();
        }
    }


    /// <summary>
    /// DelAttr Method
    /// </summary>
    ///
    /// <remarks>
    /// Delete the named attribute of the Python object, where name is a 
    /// PyObject wrapping a Python string or unicode object. This method 
    /// throws a PythonException if the attribute set fails.
    /// </remarks>

    public void DelAttr(PyObject name) {
        int r = Runtime.PyObject_SetAttr(obj, name.obj, IntPtr.Zero);
        if (r < 0) {
            throw new PythonException();
        }
    }


    /// <summary>
    /// GetItem Method
    /// </summary>
    ///
    /// <remarks>
    /// For objects that support the Python sequence or mapping protocols,
    /// return the item at the given object index. This method raises a 
    /// PythonException if the indexing operation fails.
    /// </remarks>

    public virtual PyObject GetItem(PyObject key) {
        IntPtr op = Runtime.PyObject_GetItem(obj, key.obj);
        if (op == IntPtr.Zero) {
            throw new PythonException();
        }
        return new PyObject(op);
    }


    /// <summary>
    /// GetItem Method
    /// </summary>
    ///
    /// <remarks>
    /// For objects that support the Python sequence or mapping protocols,
    /// return the item at the given string index. This method raises a 
    /// PythonException if the indexing operation fails.
    /// </remarks>

    public virtual PyObject GetItem(string key) {
        return GetItem(new PyString(key));
    }


    /// <summary>
    /// GetItem Method
    /// </summary>
    ///
    /// <remarks>
    /// For objects that support the Python sequence or mapping protocols,
    /// return the item at the given numeric index. This method raises a 
    /// PythonException if the indexing operation fails.
    /// </remarks>

    public virtual PyObject GetItem(int index) {
        PyInt key = new PyInt(index);
        return GetItem((PyObject)key);
    }


    /// <summary>
    /// SetItem Method
    /// </summary>
    ///
    /// <remarks>
    /// For objects that support the Python sequence or mapping protocols,
    /// set the item at the given object index to the given value. This 
    /// method raises a PythonException if the set operation fails.
    /// </remarks>

    public virtual void SetItem(PyObject key, PyObject value) {
        int r = Runtime.PyObject_SetItem(obj, key.obj, value.obj);
        if (r < 0) {
            throw new PythonException();
        }
    }


    /// <summary>
    /// SetItem Method
    /// </summary>
    ///
    /// <remarks>
    /// For objects that support the Python sequence or mapping protocols,
    /// set the item at the given string index to the given value. This 
    /// method raises a PythonException if the set operation fails.
    /// </remarks>

    public virtual void SetItem(string key, PyObject value) {
        SetItem(new PyString(key), value);
    }


    /// <summary>
    /// SetItem Method
    /// </summary>
    ///
    /// <remarks>
    /// For objects that support the Python sequence or mapping protocols,
    /// set the item at the given numeric index to the given value. This 
    /// method raises a PythonException if the set operation fails.
    /// </remarks>

    public virtual void SetItem(int index, PyObject value) {
        SetItem(new PyInt(index), value);
    }


    /// <summary>
    /// DelItem Method
    /// </summary>
    ///
    /// <remarks>
    /// For objects that support the Python sequence or mapping protocols,
    /// delete the item at the given object index. This method raises a 
    /// PythonException if the delete operation fails.
    /// </remarks>

    public virtual void DelItem(PyObject key) {
        int r = Runtime.PyObject_DelItem(obj, key.obj);
        if (r < 0) {
            throw new PythonException();
        }
    }


    /// <summary>
    /// DelItem Method
    /// </summary>
    ///
    /// <remarks>
    /// For objects that support the Python sequence or mapping protocols,
    /// delete the item at the given string index. This method raises a 
    /// PythonException if the delete operation fails.
    /// </remarks>

    public virtual void DelItem(string key) {
        DelItem(new PyString(key));
    }


    /// <summary>
    /// DelItem Method
    /// </summary>
    ///
    /// <remarks>
    /// For objects that support the Python sequence or mapping protocols,
    /// delete the item at the given numeric index. This method raises a 
    /// PythonException if the delete operation fails.
    /// </remarks>

    public virtual void DelItem(int index) {
        DelItem(new PyInt(index));
    }


    /// <summary>
    /// Length Method
    /// </summary>
    ///
    /// <remarks>
    /// Returns the length for objects that support the Python sequence 
    /// protocol, or 0 if the object does not support the protocol.
    /// </remarks>

    public virtual int Length() {
        int s = Runtime.PyObject_Size(obj);
        if (s < 0) {
            Runtime.PyErr_Clear();
            return 0;
        }
        return s;
    }


    /// <summary>
    /// String Indexer
    /// </summary>
    ///
    /// <remarks>
    /// Provides a shorthand for the string versions of the GetItem and 
    /// SetItem methods.
    /// </remarks>

    public virtual PyObject this[string key] {
        get { return GetItem(key); }
        set { SetItem(key, value); }
    }


    /// <summary>
    /// PyObject Indexer
    /// </summary>
    ///
    /// <remarks>
    /// Provides a shorthand for the object versions of the GetItem and 
    /// SetItem methods.
    /// </remarks>

    public virtual PyObject this[PyObject key] {
        get { return GetItem(key); }
        set { SetItem(key, value); }
    }


    /// <summary>
    /// Numeric Indexer
    /// </summary>
    ///
    /// <remarks>
    /// Provides a shorthand for the numeric versions of the GetItem and 
    /// SetItem methods.
    /// </remarks>

    public virtual PyObject this[int index] {
        get { return GetItem(index); }
        set { SetItem(index, value); }
    }


    /// <summary>
    /// GetIterator Method
    /// </summary>
    ///
    /// <remarks>
    /// Return a new (Python) iterator for the object. This is equivalent
    /// to the Python expression "iter(object)". A PythonException will be 
    /// raised if the object cannot be iterated.
    /// </remarks>

    public PyObject GetIterator() {
        IntPtr r = Runtime.PyObject_GetIter(obj);
        if (r == IntPtr.Zero) {
            throw new PythonException();
        }
        return new PyObject(r);
    }


    /// <summary>
    /// Invoke Method
    /// </summary>
    ///
    /// <remarks>
    /// Invoke the callable object with the given arguments, passed as a
    /// PyObject[]. A PythonException is raised if the invokation fails.
    /// </remarks>

    public PyObject Invoke(params PyObject[] args) {
        PyTuple t = new PyTuple(args);
        IntPtr r = Runtime.PyObject_Call(obj, t.obj, IntPtr.Zero);
        t.Dispose();
        if (r == IntPtr.Zero) {
            throw new PythonException();
        }
        return new PyObject(r);
    }


    /// <summary>
    /// Invoke Method
    /// </summary>
    ///
    /// <remarks>
    /// Invoke the callable object with the given arguments, passed as a
    /// Python tuple. A PythonException is raised if the invokation fails.
    /// </remarks>

    public PyObject Invoke(PyTuple args) {
        IntPtr r = Runtime.PyObject_Call(obj, args.obj, IntPtr.Zero);
        if (r == IntPtr.Zero) {
            throw new PythonException();
        }
        return new PyObject(r);
    }


    /// <summary>
    /// Invoke Method
    /// </summary>
    ///
    /// <remarks>
    /// Invoke the callable object with the given positional and keyword
    /// arguments. A PythonException is raised if the invokation fails.
    /// </remarks>

    public PyObject Invoke(PyObject[] args, PyDict kw) {
        PyTuple t = new PyTuple(args);
        IntPtr r = Runtime.PyObject_Call(obj, t.obj, kw.obj);
        t.Dispose();
        if (r == IntPtr.Zero) {
            throw new PythonException();
        }
        return new PyObject(r);
    }


    /// <summary>
    /// Invoke Method
    /// </summary>
    ///
    /// <remarks>
    /// Invoke the callable object with the given positional and keyword
    /// arguments. A PythonException is raised if the invokation fails.
    /// </remarks>

    public PyObject Invoke(PyTuple args, PyDict kw) {
        IntPtr r = Runtime.PyObject_Call(obj, args.obj, kw.obj);
        if (r == IntPtr.Zero) {
            throw new PythonException();
        }
        return new PyObject(r);
    }


    /// <summary>
    /// InvokeMethod Method
    /// </summary>
    ///
    /// <remarks>
    /// Invoke the named method of the object with the given arguments.
    /// A PythonException is raised if the invokation is unsuccessful.
    /// </remarks>

    public PyObject InvokeMethod(string name, params PyObject[] args) {
        PyObject method = GetAttr(name);
        PyObject result = method.Invoke(args);
        method.Dispose();
        return result;
    }


    /// <summary>
    /// InvokeMethod Method
    /// </summary>
    ///
    /// <remarks>
    /// Invoke the named method of the object with the given arguments.
    /// A PythonException is raised if the invokation is unsuccessful.
    /// </remarks>

    public PyObject InvokeMethod(string name, PyTuple args) {
        PyObject method = GetAttr(name);
        PyObject result = method.Invoke(args);
        method.Dispose();
        return result;
    }


    /// <summary>
    /// InvokeMethod Method
    /// </summary>
    ///
    /// <remarks>
    /// Invoke the named method of the object with the given arguments 
    /// and keyword arguments. Keyword args are passed as a PyDict object.
    /// A PythonException is raised if the invokation is unsuccessful.
    /// </remarks>

    public PyObject InvokeMethod(string name, PyObject[] args, PyDict kw) {
        PyObject method = GetAttr(name);
        PyObject result = method.Invoke(args, kw);
        method.Dispose();
        return result;
    }


    /// <summary>
    /// InvokeMethod Method
    /// </summary>
    ///
    /// <remarks>
    /// Invoke the named method of the object with the given arguments 
    /// and keyword arguments. Keyword args are passed as a PyDict object.
    /// A PythonException is raised if the invokation is unsuccessful.
    /// </remarks>

    public PyObject InvokeMethod(string name, PyTuple args, PyDict kw) {
        PyObject method = GetAttr(name);
        PyObject result = method.Invoke(args, kw);
        method.Dispose();
        return result;
    }


    /// <summary>
    /// IsInstance Method
    /// </summary>
    ///
    /// <remarks>
    /// Return true if the object is an instance of the given Python type
    /// or class. This method always succeeds.
    /// </remarks>

    public bool IsInstance(PyObject typeOrClass) {
        int r = Runtime.PyObject_IsInstance(obj, typeOrClass.obj);
        if (r < 0) {
            Runtime.PyErr_Clear();
            return false;
        }
        return (r != 0);
    }


    /// <summary>
    /// IsSubclass Method
    /// </summary>
    ///
    /// <remarks>
    /// Return true if the object is identical to or derived from the 
    /// given Python type or class. This method always succeeds.
    /// </remarks>

    public bool IsSubclass(PyObject typeOrClass) {
        int r = Runtime.PyObject_IsSubclass(obj, typeOrClass.obj);
        if (r < 0) {
            Runtime.PyErr_Clear();
            return false;
        }
        return (r != 0);
    }


    /// <summary>
    /// IsCallable Method
    /// </summary>
    ///
    /// <remarks>
    /// Returns true if the object is a callable object. This method 
    /// always succeeds.
    /// </remarks>

    public bool IsCallable() {
        return (Runtime.PyCallable_Check(obj) != 0);
    }


    /// <summary>
    /// IsTrue Method
    /// </summary>
    ///
    /// <remarks>
    /// Return true if the object is true according to Python semantics.
    /// This method always succeeds.
    /// </remarks>

    public bool IsTrue() {
        return (Runtime.PyObject_IsTrue(obj) != 0);
    }


    /// <summary>
    /// Dir Method
    /// </summary>
    ///
    /// <remarks>
    /// Return a list of the names of the attributes of the object. This
    /// is equivalent to the Python expression "dir(object)".
    /// </remarks>

    public PyList Dir() {
        IntPtr r = Runtime.PyObject_Dir(obj);
        if (r == IntPtr.Zero) {
            throw new PythonException();
        }
        return new PyList(r);
    }


    /// <summary>
    /// Repr Method
    /// </summary>
    ///
    /// <remarks>
    /// Return a string representation of the object. This method is 
    /// the managed equivalent of the Python expression "repr(object)".
    /// </remarks>

    public string Repr() {
        IntPtr strval = Runtime.PyObject_Repr(obj);
        string result = Runtime.GetManagedString(strval);
        Runtime.Decref(strval);
        return result;
    }


    /// <summary>
    /// ToString Method
    /// </summary>
    ///
    /// <remarks>
    /// Return the string representation of the object. This method is 
    /// the managed equivalent of the Python expression "str(object)".
    /// </remarks>

    public override string ToString() {
        IntPtr strval = Runtime.PyObject_Unicode(obj);
        string result = Runtime.GetManagedString(strval);
        Runtime.Decref(strval);
        return result;
    }


    /// <summary>
    /// Equals Method
    /// </summary>
    ///
    /// <remarks>
    /// Return true if this object is equal to the given object. This
    /// method is based on Python equality semantics.
    /// </remarks>

    public override bool Equals(object o) {
        if (!(o is PyObject)) {
            return false;
        }
        if (obj == ((PyObject) o).obj) {
            return true;
        }
        int r = Runtime.PyObject_Compare(obj, ((PyObject) o).obj);
        if (Exceptions.ErrorOccurred()) {
            throw new PythonException();
        }
        return (r == 0);
    }


    /// <summary>
    /// GetHashCode Method
    /// </summary>
    ///
    /// <remarks>
    /// Return a hashcode based on the Python object. This returns the
    /// hash as computed by Python, equivalent to the Python expression
    /// "hash(obj)".
    /// </remarks>

    public override int GetHashCode() {
        return Runtime.PyObject_Hash(obj).ToInt32();
    }


    }


}
