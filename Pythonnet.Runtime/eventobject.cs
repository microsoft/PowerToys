// ==========================================================================
// This software is subject to the provisions of the Zope Public License,
// Version 2.0 (ZPL).  A copy of the ZPL should accompany this distribution.
// THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
// WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
// FOR A PARTICULAR PURPOSE.
// ==========================================================================

using System;
using System.Collections;
using System.Reflection;

namespace Python.Runtime {

    //========================================================================
    // Implements a Python descriptor type that provides access to CLR events.
    //========================================================================

    internal class EventObject : ExtensionType {

        internal string name;
        internal EventBinding unbound;
        internal EventInfo info;
        internal Hashtable reg;

        public EventObject(EventInfo info) : base() {
            this.name = info.Name;
            this.info = info;
        }


        //====================================================================
        // Register a new Python object event handler with the event.
        //====================================================================

        internal bool AddEventHandler(IntPtr target, IntPtr handler) {
            Object obj = null;
            if (target != IntPtr.Zero) {
                CLRObject co = (CLRObject)ManagedType.GetManagedObject(target);
                obj = co.inst;
            }

            // Create a true delegate instance of the appropriate type to
            // wrap the Python handler. Note that wrapper delegate creation
            // always succeeds, though calling the wrapper may fail.

            Type type = this.info.EventHandlerType;
            Delegate d = PythonEngine.DelegateManager.GetDelegate(type, handler);

            // Now register the handler in a mapping from instance to pairs
            // of (handler hash, delegate) so we can lookup to remove later.
            // All this is done lazily to avoid overhead until an event is 
            // actually subscribed to by a Python event handler.

            if (reg == null) {
                reg = new Hashtable();
            }
            object key = (obj != null) ? obj : this.info.ReflectedType;
            ArrayList list = reg[key] as ArrayList;
            if (list == null) {
                list = new ArrayList();
                reg[key] = list;
            }
            list.Add(new Handler(Runtime.PyObject_Hash(handler), d));

            // Note that AddEventHandler helper only works for public events, 
            // so we have to get the underlying add method explicitly.

            object[] args = { d };
            MethodInfo mi = this.info.GetAddMethod(true);
            mi.Invoke(obj, BindingFlags.Default, null, args, null);

            return true;
        }


        //====================================================================
        // Remove the given Python object event handler.
        //====================================================================

        internal bool RemoveEventHandler(IntPtr target, IntPtr handler) {
            Object obj = null;
            if (target != IntPtr.Zero) {
                CLRObject co = (CLRObject)ManagedType.GetManagedObject(target);
                obj = co.inst;
            }

            IntPtr hash = Runtime.PyObject_Hash(handler);
            if (Exceptions.ErrorOccurred() || (reg == null)) {
                Exceptions.SetError(Exceptions.ValueError, 
                                    "unknown event handler"
                                    ); 
                return false;
            }

            object key = (obj != null) ? obj : this.info.ReflectedType;
            ArrayList list = reg[key] as ArrayList;

            if (list == null) {
                Exceptions.SetError(Exceptions.ValueError, 
                                    "unknown event handler"
                                    ); 
                return false;
            }

            object[] args = { null };
            MethodInfo mi = this.info.GetRemoveMethod(true);

            for (int i = 0; i < list.Count; i++) {
                Handler item = (Handler)list[i];
                if (item.hash != hash) {
                    continue;
                }
                args[0] = item.del;
                try {
                    mi.Invoke(obj, BindingFlags.Default, null, args, null);
                }
                catch {
                    continue;
                }
                list.RemoveAt(i);
                return true;
            }

            Exceptions.SetError(Exceptions.ValueError, 
                                "unknown event handler"
                                ); 
            return false;
        }


        //====================================================================
        // Descriptor __get__ implementation. A getattr on an event returns
        // a "bound" event that keeps a reference to the object instance.
        //====================================================================

        public static IntPtr tp_descr_get(IntPtr ds, IntPtr ob, IntPtr tp) {
            EventObject self = GetManagedObject(ds) as EventObject;
            EventBinding binding;

            if (self == null) {
                return Exceptions.RaiseTypeError("invalid argument");
            }

            // If the event is accessed through its type (rather than via
            // an instance) we return an 'unbound' EventBinding that will
            // be cached for future accesses through the type.

            if (ob == IntPtr.Zero) {
                if (self.unbound == null) {
                    self.unbound = new EventBinding(self, IntPtr.Zero);
                }
                binding = self.unbound;
                Runtime.Incref(binding.pyHandle);
                return binding.pyHandle;
            }

            if (Runtime.PyObject_IsInstance(ob, tp) < 1) {
                return Exceptions.RaiseTypeError("invalid argument");
            }

            binding = new EventBinding(self, ob);
            return binding.pyHandle;
        }


        //====================================================================
        // Descriptor __set__ implementation. This actually never allows you
        // to set anything; it exists solely to support the '+=' spelling of
        // event handler registration. The reason is that given code like:
        // 'ob.SomeEvent += method', Python will attempt to set the attribute
        // SomeEvent on ob to the result of the '+=' operation.
        //====================================================================

        public static new int tp_descr_set(IntPtr ds, IntPtr ob, IntPtr val) {
            EventBinding e = GetManagedObject(val) as EventBinding;

            if (e != null) {
                return 0;
            }

            string message = "cannot set event attributes";
            Exceptions.RaiseTypeError(message);
            return -1;
        }


        //====================================================================
        // Descriptor __repr__ implementation.
        //====================================================================

        public static IntPtr tp_repr(IntPtr ob) {
            EventObject self = (EventObject)GetManagedObject(ob);
            string s = String.Format("<event '{0}'>", self.name);
            return Runtime.PyString_FromString(s);
        }


        //====================================================================
        // Descriptor dealloc implementation.
        //====================================================================

        public static new void tp_dealloc(IntPtr ob) {
            EventObject self = (EventObject)GetManagedObject(ob);
            if (self.unbound != null) {
                Runtime.Decref(self.unbound.pyHandle);
            }
            ExtensionType.FinalizeObject(self);
        }


    }



    internal class Handler {

        public IntPtr hash;
        public Delegate del;

        public Handler(IntPtr hash, Delegate d) {
            this.hash = hash;
            this.del = d;
        }

    }


}
