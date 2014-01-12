// ==========================================================================
// This software is subject to the provisions of the Zope Public License,
// Version 2.0 (ZPL).  A copy of the ZPL should accompany this distribution.
// THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
// WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
// FOR A PARTICULAR PURPOSE.
// ==========================================================================

using System;
using System.IO;
using System.Collections;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace Python.Runtime {

    /// <summary>
    /// The AssemblyManager maintains information about loaded assemblies  
    /// namespaces and provides an interface for name-based type lookup.
    /// </summary>

    internal class AssemblyManager {

        static Dictionary<string, Dictionary<Assembly, string>> namespaces;
        //static Dictionary<string, Dictionary<string, string>> generics;
        static AssemblyLoadEventHandler lhandler;
        static ResolveEventHandler rhandler;
        static Dictionary<string, int> probed;
        static List<Assembly> assemblies;
        internal static List<string> pypath;

        private AssemblyManager() {}

        //===================================================================
        // Initialization performed on startup of the Python runtime. Here we
        // scan all of the currently loaded assemblies to determine exported
        // names, and register to be notified of new assembly loads.
        //===================================================================

        internal static void Initialize() {
            namespaces = new 
                         Dictionary<string, Dictionary<Assembly, string>>(32);
            probed = new Dictionary<string, int>(32);
            //generics = new Dictionary<string, Dictionary<string, string>>();
            assemblies = new List<Assembly>(16);
            pypath = new List<string>(16);

            AppDomain domain = AppDomain.CurrentDomain;

            lhandler = new AssemblyLoadEventHandler(AssemblyLoadHandler);
            domain.AssemblyLoad += lhandler;

            rhandler = new ResolveEventHandler(ResolveHandler);        
            domain.AssemblyResolve += rhandler;

            Assembly[] items = domain.GetAssemblies();
            for (int i = 0; i < items.Length; i++) {
                Assembly a = items[i];
                assemblies.Add(a);
                ScanAssembly(a);
            }
        }


        //===================================================================
        // Cleanup resources upon shutdown of the Python runtime.
        //===================================================================

        internal static void Shutdown() {
            AppDomain domain = AppDomain.CurrentDomain;
            domain.AssemblyLoad -= lhandler;
            domain.AssemblyResolve -= rhandler;
        }


        //===================================================================
        // Event handler for assembly load events. At the time the Python 
        // runtime loads, we scan the app domain to map the assemblies that
        // are loaded at the time. We also have to register this event handler
        // so that we can know about assemblies that get loaded after the 
        // Python runtime is initialized.
        //===================================================================

        static void AssemblyLoadHandler(Object ob, AssemblyLoadEventArgs args){
            Assembly assembly = args.LoadedAssembly;
            assemblies.Add(assembly);
            ScanAssembly(assembly);
        }


        //===================================================================
        // Event handler for assembly resolve events. This is needed because
        // we augment the assembly search path with the PYTHONPATH when we
        // load an assembly from Python. Because of that, we need to listen
        // for failed loads, because they might be dependencies of something
        // we loaded from Python which also needs to be found on PYTHONPATH.
        //===================================================================

        static Assembly ResolveHandler(Object ob, ResolveEventArgs args){
            string name = args.Name.ToLower();
            for (int i = 0; i < assemblies.Count; i++) {
                Assembly a = (Assembly)assemblies[i];
                string full = a.FullName.ToLower();
                if (full.StartsWith(name)) {
                    return a;
                }
            }
            return LoadAssemblyPath(args.Name);
        }


        //===================================================================
        // We __really__ want to avoid using Python objects or APIs when
        // probing for assemblies to load, since our ResolveHandler may be 
        // called in contexts where we don't have the Python GIL and can't
        // even safely try to get it without risking a deadlock ;(
        //
        // To work around that, we update a managed copy of sys.path (which 
        // is the main thing we care about) when UpdatePath is called. The
        // import hook calls this whenever it knows its about to use the
        // assembly manager, which lets us keep up with changes to sys.path
        // in a relatively lightweight and low-overhead way.
        //===================================================================

        internal static void UpdatePath() {
            IntPtr list = Runtime.PySys_GetObject("path");
            int count = Runtime.PyList_Size(list);
            if (count != pypath.Count) {
                pypath.Clear();
                probed.Clear();
                for (int i = 0; i < count; i++) {
                    IntPtr item = Runtime.PyList_GetItem(list, i);
                    string path = Runtime.GetManagedString(item);
                    if (path != null) {
                        pypath.Add(path);
                    }
                }
            }
        }


        //===================================================================
        // Given an assembly name, try to find this assembly file using the
        // PYTHONPATH. If not found, return null to indicate implicit load
        // using standard load semantics (app base directory then GAC, etc.)
        //===================================================================

        public static string FindAssembly(string name) {
            char sep = Path.DirectorySeparatorChar;
            string path;
            string temp;

            for (int i = 0; i < pypath.Count; i++) {
                string head = pypath[i];
                if (head == null || head.Length == 0) {
                    path = name;
                }
                else {
                    path = head + sep + name;
                }

                temp = path + ".dll";
                if (File.Exists(temp)) {
                    return temp;
                }
                temp = path + ".exe";
                if (File.Exists(temp)) {
                    return temp;
                }
            }
            return null;
        }


        //===================================================================
        // Loads an assembly from the application directory or the GAC
        // given a simple assembly name. Returns the assembly if loaded.
        //===================================================================

        public static Assembly LoadAssembly(string name) {
            Assembly assembly = null;
            try {
                assembly = Assembly.Load(name);
            }
            catch { }
            return assembly;
        }


        //===================================================================
        // Loads an assembly using an augmented search path (the python path).
        //===================================================================

        public static Assembly LoadAssemblyPath(string name) {
            string path = FindAssembly(name);
            Assembly assembly = null;
            if (path != null) {
                try   { assembly = Assembly.LoadFrom(path); }
                catch {}
            }
            return assembly;
        }


        //===================================================================
        // Given a qualified name of the form A.B.C.D, attempt to load 
        // an assembly named after each of A.B.C.D, A.B.C, A.B, A. This
        // will only actually probe for the assembly once for each unique
        // namespace. Returns true if any assemblies were loaded.
        // TODO item 3 "* Deprecate implicit loading of assemblies":
        //  Set the fromFile flag if the name of the loaded assembly matches
        //  the fully qualified name that was requested if the framework
        //  actually loads an assembly.
        // Call ONLY for namespaces that HAVE NOT been cached yet.
        //===================================================================

        public static bool LoadImplicit(string name, out bool fromFile) {
            // 2010-08-16: Deprecation support
            // Added out param to detect fully qualified name load
            fromFile = false;
            string[] names = name.Split('.');
            bool loaded = false;
            string s = "";
            for (int i = 0; i < names.Length; i++) {
                s = (i == 0) ? names[0] : s + "." + names[i];
                if (!probed.ContainsKey(s)) {
                    if (LoadAssemblyPath(s) != null) {
                        loaded = true;
                        /* 2010-08-16: Deprecation support */
                        if (s == name) {
                            fromFile = true;
                        }
                    }
                    else if (LoadAssembly(s) != null) {
                        loaded = true;
                        /* 2010-08-16: Deprecation support */
                        if (s == name) {
                            fromFile = true;
                        }
                    }
                    probed[s] = 1;
                    // 2010-12-24: Deprecation logic
                    /* if (loaded && (s == name)) {
                        fromFile = true;
                        break;
                    } */
                }
            }
            return loaded;
        }


        //===================================================================
        // Scans an assembly for exported namespaces, adding them to the
        // mapping of valid namespaces. Note that for a given namespace
        // a.b.c.d, each of a, a.b, a.b.c and a.b.c.d are considered to 
        // be valid namespaces (to better match Python import semantics).
        //===================================================================

        static void ScanAssembly(Assembly assembly) {

            // A couple of things we want to do here: first, we want to
            // gather a list of all of the namespaces contributed to by
            // the assembly.

            Type[] types = assembly.GetTypes();
            for (int i = 0; i < types.Length; i++) {
                Type t = types[i];
                string ns = t.Namespace;
                if ((ns != null) && (!namespaces.ContainsKey(ns))) {
                    string[] names = ns.Split('.');
                    string s = "";
                    for (int n = 0; n < names.Length; n++) {
                        s = (n == 0) ? names[0] : s + "." + names[n];
                        if (!namespaces.ContainsKey(s)) {
                            namespaces.Add(s,
                                           new Dictionary<Assembly, string>()
                                           );
                        }
                    }
                }

                if (ns != null && !namespaces[ns].ContainsKey(assembly)) {
                    namespaces[ns].Add(assembly, String.Empty);
                }

                if (t.IsGenericTypeDefinition) {
                    GenericUtil.Register(t);
                }
            }
        }

        public static AssemblyName[] ListAssemblies()
        {
            AssemblyName[] names = new AssemblyName[assemblies.Count];
            Assembly assembly;
            for (int i=0; i < assemblies.Count; i++)
            {
                assembly = assemblies[i];
                names.SetValue(assembly.GetName(), i);
            }
            return names;
        }

        //===================================================================
        // Returns true if the given qualified name matches a namespace
        // exported by an assembly loaded in the current app domain.
        //===================================================================

        public static bool IsValidNamespace(string name) {
            return namespaces.ContainsKey(name);
        }


        //===================================================================
        // Returns the current list of valid names for the input namespace.
        //===================================================================

        public static List<string> GetNames(string nsname) {
            //Dictionary<string, int> seen = new Dictionary<string, int>();
            List<string> names = new List<string>(8);

            List<string> g = GenericUtil.GetGenericBaseNames(nsname);
            if (g != null) {
                foreach (string n in g) {
                    names.Add(n);
                }
            }

            if (namespaces.ContainsKey(nsname)) {
                foreach (Assembly a in namespaces[nsname].Keys) {
                    Type[] types = a.GetTypes();
                    for (int i = 0; i < types.Length; i++) {
                        Type t = types[i];
                        if (t.Namespace == nsname) {
                            names.Add(t.Name);
                        }
                    }
                }
                int nslen = nsname.Length;
                foreach (string key in namespaces.Keys) {
                    if (key.Length > nslen && key.StartsWith(nsname)) {
                        //string tail = key.Substring(nslen);
                        if (key.IndexOf('.') == -1) {
                            names.Add(key);
                        } 
                    }
                }
            }
            return names;
        }

        //===================================================================
        // Returns the System.Type object for a given qualified name,
        // looking in the currently loaded assemblies for the named
        // type. Returns null if the named type cannot be found.
        //===================================================================

        public static Type LookupType(string qname) {
            for (int i = 0; i < assemblies.Count; i++) {
                Assembly assembly = (Assembly)assemblies[i];
                Type type = assembly.GetType(qname);
                if (type != null) {
                    return type;
                }
            }
            return null;
        }

    }


}
