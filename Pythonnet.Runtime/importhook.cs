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
using System.Runtime.InteropServices;

namespace Python.Runtime {

    //========================================================================
    // Implements the "import hook" used to integrate Python with the CLR.
    //========================================================================

    internal class ImportHook {

        static IntPtr py_import;
        static CLRModule root;
        static MethodWrapper hook;

        //===================================================================
        // Initialization performed on startup of the Python runtime.
        //===================================================================

        internal static void Initialize() {

            // Initialize the Python <--> CLR module hook. We replace the
            // built-in Python __import__ with our own. This isn't ideal, 
            // but it provides the most "Pythonic" way of dealing with CLR
            // modules (Python doesn't provide a way to emulate packages).

            IntPtr dict = Runtime.PyImport_GetModuleDict();
            IntPtr mod = Runtime.PyDict_GetItemString(dict, "__builtin__");
            py_import = Runtime.PyObject_GetAttrString(mod, "__import__");

              hook = new MethodWrapper(typeof(ImportHook), "__import__");
              Runtime.PyObject_SetAttrString(mod, "__import__", hook.ptr);
            Runtime.Decref(hook.ptr);

            root = new CLRModule();
            Runtime.Incref(root.pyHandle); // we are using the module two times
            Runtime.PyDict_SetItemString(dict, "CLR", root.pyHandle);
            Runtime.PyDict_SetItemString(dict, "clr", root.pyHandle);
        }


        //===================================================================
        // Cleanup resources upon shutdown of the Python runtime.
        //===================================================================

        internal static void Shutdown() {
            Runtime.Decref(root.pyHandle);
            Runtime.Decref(root.pyHandle);
            Runtime.Decref(py_import);
        }


        //===================================================================
        // The actual import hook that ties Python to the managed world.
        //===================================================================

        public static IntPtr __import__(IntPtr self, IntPtr args, IntPtr kw) {

            // Replacement for the builtin __import__. The original import
            // hook is saved as this.py_import. This version handles CLR 
            // import and defers to the normal builtin for everything else.

            int num_args = Runtime.PyTuple_Size(args);
            if (num_args < 1) {
                return Exceptions.RaiseTypeError(
                       "__import__() takes at least 1 argument (0 given)"
                       );
            }

            // borrowed reference
            IntPtr py_mod_name = Runtime.PyTuple_GetItem(args, 0);
            if ((py_mod_name == IntPtr.Zero) ||
               (!Runtime.IsStringType(py_mod_name))) {
                return Exceptions.RaiseTypeError("string expected");
            }

            // Check whether the import is of the form 'from x import y'.
            // This determines whether we return the head or tail module.

            IntPtr fromList = IntPtr.Zero;
            bool fromlist = false;
            if (num_args >= 4) {
                fromList = Runtime.PyTuple_GetItem(args, 3);
                if ((fromList != IntPtr.Zero) && 
                    (Runtime.PyObject_IsTrue(fromList) == 1)) {
                    fromlist = true;
                }
            }

            string mod_name = Runtime.GetManagedString(py_mod_name);
            // Check these BEFORE the built-in import runs; may as well
            // do the Incref()ed return here, since we've already found
            // the module.
            if (mod_name == "clr") {
                root.InitializePreload();
                Runtime.Incref(root.pyHandle);
                return root.pyHandle;
            }
            if (mod_name == "CLR") {
                Exceptions.deprecation("The CLR module is deprecated. " +
                    "Please use 'clr'.");
                root.InitializePreload();
                Runtime.Incref(root.pyHandle);
                return root.pyHandle;
            }
            string realname = mod_name;
            if (mod_name.StartsWith("CLR.")) {
                realname = mod_name.Substring(4);
                string msg = String.Format("Importing from the CLR.* namespace "+
                    "is deprecated. Please import '{0}' directly.", realname);
                Exceptions.deprecation(msg);
            }
            else {
                // 2010-08-15: Always seemed smart to let python try first...
                // This shaves off a few tenths of a second on test_module.py
                // and works around a quirk where 'sys' is found by the 
                // LoadImplicit() deprecation logic.
                // Turns out that the AssemblyManager.ResolveHandler() checks to see if any
                // Assembly's FullName.ToLower().StartsWith(name.ToLower()), which makes very
                // little sense to me.
                IntPtr res = Runtime.PyObject_Call(py_import, args, kw);
                if (res != IntPtr.Zero) {
                    // There was no error.
                    return res;
                }
                // There was an error
                if (!Exceptions.ExceptionMatches(Exceptions.ImportError)) {
                    // and it was NOT an ImportError; bail out here.
                    return IntPtr.Zero;
                }
                // Otherwise,  just clear the it.
                Exceptions.Clear();
            }

            string[] names = realname.Split('.');

            // Now we need to decide if the name refers to a CLR module,
            // and may have to do an implicit load (for b/w compatibility)
            // using the AssemblyManager. The assembly manager tries
            // really hard not to use Python objects or APIs, because 
            // parts of it can run recursively and on strange threads.
            // 
            // It does need an opportunity from time to time to check to 
            // see if sys.path has changed, in a context that is safe. Here
            // we know we have the GIL, so we'll let it update if needed.

            AssemblyManager.UpdatePath();
            if (!AssemblyManager.IsValidNamespace(realname)) {
                bool fromFile = false;
                if (AssemblyManager.LoadImplicit(realname, out fromFile)) {
                    if (true == fromFile) {
                        string deprWarning = String.Format("\nThe module was found, but not in a referenced namespace.\n" +
                                     "Implicit loading is deprecated. Please use clr.AddReference(\"{0}\").", realname);
                        Exceptions.deprecation(deprWarning);
                    }
                }
                else
                {
                    // May be called when a module being imported imports a module.
                    // In particular, I've seen decimal import copy import org.python.core
                    return Runtime.PyObject_Call(py_import, args, kw);
                }
            }

            // See if sys.modules for this interpreter already has the
            // requested module. If so, just return the exising module.

            IntPtr modules = Runtime.PyImport_GetModuleDict();
            IntPtr module = Runtime.PyDict_GetItem(modules, py_mod_name);

            if (module != IntPtr.Zero) {
                if (fromlist) {
                    Runtime.Incref(module);
                    return module;
                }
                module = Runtime.PyDict_GetItemString(modules, names[0]);
                Runtime.Incref(module);
                return module;
            }
            Exceptions.Clear();

            // Traverse the qualified module name to get the named module
            // and place references in sys.modules as we go. Note that if
            // we are running in interactive mode we pre-load the names in 
            // each module, which is often useful for introspection. If we
            // are not interactive, we stick to just-in-time creation of
            // objects at lookup time, which is much more efficient.
            // NEW: The clr got a new module variable preload. You can
            // enable preloading in a non-interactive python processing by
            // setting clr.preload = True

            ModuleObject head = (mod_name == realname) ? null : root;
            ModuleObject tail = root;
            root.InitializePreload();

            for (int i = 0; i < names.Length; i++) {
                string name = names[i];
                ManagedType mt = tail.GetAttribute(name, true);
                if (!(mt is ModuleObject)) {
                    string error = String.Format("No module named {0}", name);
                    Exceptions.SetError(Exceptions.ImportError, error); 
                    return IntPtr.Zero;
                }
                if (head == null) {
                    head = (ModuleObject)mt;
                }
                tail = (ModuleObject) mt;
                if (CLRModule.preload) {
                    tail.LoadNames();
                }
                Runtime.PyDict_SetItemString(modules, tail.moduleName, 
                                             tail.pyHandle
                                             );
            }

            ModuleObject mod = fromlist ? tail : head;

            if (fromlist && Runtime.PySequence_Size(fromList) == 1) {
                IntPtr fp = Runtime.PySequence_GetItem(fromList, 0);
                if ((!CLRModule.preload) && Runtime.GetManagedString(fp) == "*") {
                    mod.LoadNames();
                }
                Runtime.Decref(fp);
            }

            Runtime.Incref(mod.pyHandle);
            return mod.pyHandle;
        }

    }


}
