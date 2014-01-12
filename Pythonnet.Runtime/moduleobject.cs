// ==========================================================================
// This software is subject to the provisions of the Zope Public License,
// Version 2.0 (ZPL).  A copy of the ZPL should accompany this distribution.
// THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
// WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
// FOR A PARTICULAR PURPOSE.
// ==========================================================================

using System;
using System.Collections.Specialized;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Collections;
using System.Reflection;

namespace Python.Runtime {

    //========================================================================
    // Implements a Python type that provides access to CLR namespaces. The 
    // type behaves like a Python module, and can contain other sub-modules.
    //========================================================================

    internal class ModuleObject : ExtensionType {

        Dictionary<string, ManagedType> cache;
        internal string moduleName;
        internal IntPtr dict;
        protected string _namespace;

        public ModuleObject(string name) : base() {
            if (name == String.Empty)
            {
                throw new ArgumentException("Name must not be empty!");
            }
            moduleName = name;
            cache = new Dictionary<string, ManagedType>();
            _namespace = name;

            dict = Runtime.PyDict_New();
            IntPtr pyname = Runtime.PyString_FromString(moduleName);
            Runtime.PyDict_SetItemString(dict, "__name__", pyname);
            Runtime.PyDict_SetItemString(dict, "__file__", Runtime.PyNone);
            Runtime.PyDict_SetItemString(dict, "__doc__", Runtime.PyNone);
            Runtime.Decref(pyname);

            Marshal.WriteIntPtr(this.pyHandle, ObjectOffset.ob_dict, dict);

            InitializeModuleMembers();
        }


        //===================================================================
        // Returns a ClassBase object representing a type that appears in
        // this module's namespace or a ModuleObject representing a child 
        // namespace (or null if the name is not found). This method does
        // not increment the Python refcount of the returned object.
        //===================================================================

        public ManagedType GetAttribute(string name, bool guess) {
            ManagedType cached = null;
            this.cache.TryGetValue(name, out cached);
            if (cached != null) {
                return cached;
            }

            ModuleObject m;
            ClassBase c;
            Type type;

            //if (AssemblyManager.IsValidNamespace(name))
            //{
            //    IntPtr py_mod_name = Runtime.PyString_FromString(name);
            //    IntPtr modules = Runtime.PyImport_GetModuleDict();
            //    IntPtr module = Runtime.PyDict_GetItem(modules, py_mod_name);
            //    if (module != IntPtr.Zero)
            //        return (ManagedType)this;
            //    return null;
            //}

            string qname = (_namespace == String.Empty) ? name : 
                            _namespace + "." + name;

            // If the fully-qualified name of the requested attribute is 
            // a namespace exported by a currently loaded assembly, return 
            // a new ModuleObject representing that namespace.

            if (AssemblyManager.IsValidNamespace(qname)) {
                m = new ModuleObject(qname);
                StoreAttribute(name, m);
                return (ManagedType) m;
            }

            // Look for a type in the current namespace. Note that this 
            // includes types, delegates, enums, interfaces and structs.
            // Only public namespace members are exposed to Python.

            type = AssemblyManager.LookupType(qname);
            if (type != null) {
                if (!type.IsPublic) {
                    return null;
                }
                c = ClassManager.GetClass(type);
                StoreAttribute(name, c);
                return (ManagedType) c;
            }

            // This is a little repetitive, but it ensures that the right
            // thing happens with implicit assembly loading at a reasonable
            // cost. Ask the AssemblyManager to do implicit loading for each 
            // of the steps in the qualified name, then try it again.
            bool fromFile;
            if (AssemblyManager.LoadImplicit(qname, out fromFile)) {
                bool ignore = name.StartsWith("__");
                if (true == fromFile && (!ignore)) {
                    string deprWarning = String.Format("\nThe module was found, but not in a referenced namespace.\n" +
                                 "Implicit loading is deprecated. Please use clr.AddReference(\"{0}\").", qname);
                    Exceptions.deprecation(deprWarning);
                }
                if (AssemblyManager.IsValidNamespace(qname)) {
                    m = new ModuleObject(qname);
                    StoreAttribute(name, m);
                    return (ManagedType) m;
                }

                type = AssemblyManager.LookupType(qname);
                if (type != null) {
                    if (!type.IsPublic) {
                        return null;
                    }
                    c = ClassManager.GetClass(type);
                    StoreAttribute(name, c);
                    return (ManagedType) c;
                }
            }

            // We didn't find the name, so we may need to see if there is a 
            // generic type with this base name. If so, we'll go ahead and
            // return it. Note that we store the mapping of the unmangled
            // name to generic type -  it is technically possible that some
            // future assembly load could contribute a non-generic type to
            // the current namespace with the given basename, but unlikely
            // enough to complicate the implementation for now.

            if (guess) {
                string gname = GenericUtil.GenericNameForBaseName(
                                              _namespace, name);
                if (gname != null) {
                    ManagedType o = GetAttribute(gname, false);
                    if (o != null) {
                        StoreAttribute(name, o);
                        return o;
                    }
                }
            }

            return null;
        }


        //===================================================================
        // Stores an attribute in the instance dict for future lookups.
         //===================================================================

        private void StoreAttribute(string name, ManagedType ob) {
            Runtime.PyDict_SetItemString(dict, name, ob.pyHandle);
            cache[name] = ob;
        }


        //===================================================================
        // Preloads all currently-known names for the module namespace. This
        // can be called multiple times, to add names from assemblies that
        // may have been loaded since the last call to the method.
         //===================================================================

        public void LoadNames() {
            ManagedType m = null;
            foreach (string name in AssemblyManager.GetNames(_namespace)) {
                this.cache.TryGetValue(name, out m);
                if (m == null) {
                    ManagedType attr = this.GetAttribute(name, true);
                    if (Runtime.wrap_exceptions) {
                        if (attr is ExceptionClassObject) {
                            ExceptionClassObject c = attr as ExceptionClassObject;
                            if (c != null) {
                              IntPtr p = attr.pyHandle;
                              IntPtr r =Exceptions.GetExceptionClassWrapper(p);
                              Runtime.PyDict_SetItemString(dict, name, r);
                              Runtime.Incref(r);

                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Initialize module level functions and attributes
        /// </summary>
        internal void InitializeModuleMembers()
        {
            Type funcmarker = typeof(ModuleFunctionAttribute);
            Type propmarker = typeof(ModulePropertyAttribute);
            Type ftmarker = typeof(ForbidPythonThreadsAttribute);
            Type type = this.GetType();

            BindingFlags flags = BindingFlags.Public | BindingFlags.Static;

            while (type != null)
            {
                MethodInfo[] methods = type.GetMethods(flags);
                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    object[] attrs = method.GetCustomAttributes(funcmarker, false);
                    object[] forbid = method.GetCustomAttributes(ftmarker, false);
                    bool allow_threads = (forbid.Length == 0);
                    if (attrs.Length > 0)
                    {
                        string name = method.Name;
                        MethodInfo[] mi = new MethodInfo[1];
                        mi[0] = method;
                        ModuleFunctionObject m = new ModuleFunctionObject(name, mi, allow_threads);
                        StoreAttribute(name, m);
                    }
                }

                PropertyInfo[] properties = type.GetProperties();
                for (int i = 0; i < properties.Length; i++)
                {
                    PropertyInfo property = properties[i];
                    object[] attrs = property.GetCustomAttributes(propmarker, false);
                    if (attrs.Length > 0)
                    {
                        string name = property.Name;
                        ModulePropertyObject p = new ModulePropertyObject(property);
                        StoreAttribute(name, p);
                    }
                }
                type = type.BaseType;
            }
        }


        //====================================================================
        // ModuleObject __getattribute__ implementation. Module attributes
        // are always either classes or sub-modules representing subordinate 
        // namespaces. CLR modules implement a lazy pattern - the sub-modules
        // and classes are created when accessed and cached for future use.
        //====================================================================

        public static IntPtr tp_getattro(IntPtr ob, IntPtr key) {
            ModuleObject self = (ModuleObject)GetManagedObject(ob);

            if (!Runtime.PyString_Check(key)) {
                Exceptions.SetError(Exceptions.TypeError, "string expected");
                return IntPtr.Zero;
            }

            IntPtr op = Runtime.PyDict_GetItem(self.dict, key);
            if (op != IntPtr.Zero) {
                Runtime.Incref(op);
                return op;
            }
 
            string name = Runtime.GetManagedString(key);
            if (name == "__dict__") {
                Runtime.Incref(self.dict);
                return self.dict;
            }

            ManagedType attr = self.GetAttribute(name, true);

            if (attr == null) {
                Exceptions.SetError(Exceptions.AttributeError, name);
                return IntPtr.Zero;                
            }

            // XXX - hack required to recognize exception types. These types
            // may need to be wrapped in old-style class wrappers in versions
            // of Python where new-style classes cannot be used as exceptions.

            if (Runtime.wrap_exceptions) {
                if (attr is ExceptionClassObject) {
                    ExceptionClassObject c = attr as ExceptionClassObject;
                    if (c != null) {
                        IntPtr p = attr.pyHandle;
                        IntPtr r = Exceptions.GetExceptionClassWrapper(p);
                        Runtime.PyDict_SetItemString(self.dict, name, r);
                        Runtime.Incref(r);
                        return r;
                    }
                }
            }

            Runtime.Incref(attr.pyHandle);
            return attr.pyHandle;
        }

        //====================================================================
        // ModuleObject __repr__ implementation.
        //====================================================================

        public static IntPtr tp_repr(IntPtr ob) {
            ModuleObject self = (ModuleObject)GetManagedObject(ob);
            string s = String.Format("<module '{0}'>", self.moduleName);
            return Runtime.PyString_FromString(s);
        }



    }

    /// <summary>
    /// The CLR module is the root handler used by the magic import hook
    /// to import assemblies. It has a fixed module name "clr" and doesn't
    /// provide a namespace.
    /// </summary>
    internal class CLRModule : ModuleObject
    {
        protected static bool hacked = false;
        protected static bool interactive_preload = true;
        internal static bool preload;
        // XXX Test performance of new features //
        internal static bool _SuppressDocs = false;
        internal static bool _SuppressOverloads = false;

        public CLRModule() : base("clr") {
            _namespace = String.Empty;
            
            // This hackery is required in order to allow a plain Python to
            // import the managed runtime via the CLR bootstrapper module. 
            // The standard Python machinery in control at the time of the
            // import requires the module to pass PyModule_Check. :(
            if (!hacked)
            {
                IntPtr type = this.tpHandle;
                IntPtr mro = Marshal.ReadIntPtr(type, TypeOffset.tp_mro);
                IntPtr ext = Runtime.ExtendTuple(mro, Runtime.PyModuleType);
                Marshal.WriteIntPtr(type, TypeOffset.tp_mro, ext);
                Runtime.Decref(mro);
                hacked = true;
            }
        }

        /// <summary>
        /// The initializing of the preload hook has to happen as late as
        /// possible since sys.ps1 is created after the CLR module is 
        /// created.
        /// </summary>
        internal void InitializePreload() {
            if (interactive_preload) {
                interactive_preload = false;
                if (Runtime.PySys_GetObject("ps1") != IntPtr.Zero) {
                    preload = true;
                } else {
                    Exceptions.Clear();
                    preload = false;
                }
            }
        }

        [ModuleFunctionAttribute()]
        public static bool getPreload() {
            return preload;
        }

        [ModuleFunctionAttribute()]
        public static void setPreload(bool preloadFlag)
        {
            preload = preloadFlag;
        }

        //[ModulePropertyAttribute]
        public static bool SuppressDocs {
            get { return _SuppressDocs; }
            set { _SuppressDocs = value; }
        }

        //[ModulePropertyAttribute]
        public static bool SuppressOverloads {
            get { return _SuppressOverloads; }
            set { _SuppressOverloads = value; }
        }

        [ModuleFunctionAttribute()]
        [ForbidPythonThreadsAttribute()]
        public static Assembly AddReference(string name)
        {
            AssemblyManager.UpdatePath();
            Assembly assembly = null;
            assembly = AssemblyManager.LoadAssemblyPath(name);
            if (assembly == null)
            {
                assembly = AssemblyManager.LoadAssembly(name);
            }
            if (assembly == null)
            {
                string msg = String.Format("Unable to find assembly '{0}'.", name);
                throw new System.IO.FileNotFoundException(msg);
            }
            return assembly ;
        }

        [ModuleFunctionAttribute()]
        [ForbidPythonThreadsAttribute()]
        public static string FindAssembly(string name)
        {
            AssemblyManager.UpdatePath();
            return AssemblyManager.FindAssembly(name);
        }

        [ModuleFunctionAttribute()]
        public static String[] ListAssemblies(bool verbose)
        {
            AssemblyName[] assnames = AssemblyManager.ListAssemblies();
            String[] names = new String[assnames.Length];
            for (int i = 0; i < assnames.Length; i++)
            {
                if (verbose)
                    names[i] = assnames[i].FullName;
                else
                    names[i] = assnames[i].Name;
            }
            return names;
        }

    }

}
