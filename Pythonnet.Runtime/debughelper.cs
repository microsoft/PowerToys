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
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Threading;

namespace Python.Runtime {

    /// <summary>
    /// Debugging helper utilities.
    /// The methods are only executed when the DEBUG flag is set. Otherwise
    /// they are automagically hidden by the compiler and silently surpressed.
    /// </summary>

    internal class DebugUtil {

        [Conditional("DEBUG")]
        public static void Print(string msg, params IntPtr[] args) {
            string result = msg;
            result += " ";

            for (int i = 0; i < args.Length; i++) {
                if (args[i] == IntPtr.Zero) {
                    Console.WriteLine("null arg to print");
                }
                IntPtr ob = Runtime.PyObject_Repr(args[i]);
                result += Runtime.GetManagedString(ob);
                Runtime.Decref(ob);
                result += " ";
            }
            Console.WriteLine(result);
            return;
        }

        [Conditional("DEBUG")]
        public static void Print(string msg) {
            Console.WriteLine(msg);
        }

        [Conditional("DEBUG")]
        internal static void DumpType(IntPtr type) {
            IntPtr op = Marshal.ReadIntPtr(type, TypeOffset.tp_name);
            string name = Marshal.PtrToStringAnsi(op);

            Console.WriteLine("Dump type: {0}", name);

            op = Marshal.ReadIntPtr(type, TypeOffset.ob_type);
            DebugUtil.Print("  type: ", op);

            op = Marshal.ReadIntPtr(type, TypeOffset.tp_base);
            DebugUtil.Print("  base: ", op);

            op = Marshal.ReadIntPtr(type, TypeOffset.tp_bases);
            DebugUtil.Print("  bases: ", op);

            //op = Marshal.ReadIntPtr(type, TypeOffset.tp_mro);
            //DebugUtil.Print("  mro: ", op);


            FieldInfo[] slots = typeof(TypeOffset).GetFields();
            int size = IntPtr.Size;

            for (int i = 0; i < slots.Length; i++) {
                int offset = i * size;
                name = slots[i].Name;
                op = Marshal.ReadIntPtr(type, offset);
                Console.WriteLine("  {0}: {1}", name, op);
            }

            Console.WriteLine("");
            Console.WriteLine("");

            op = Marshal.ReadIntPtr(type, TypeOffset.tp_dict);
            if (op == IntPtr.Zero) {
                Console.WriteLine("  dict: null");
            }
            else {
                DebugUtil.Print("  dict: ", op);
            }

        }

        [Conditional("DEBUG")]
        internal static void DumpInst(IntPtr ob) {
            IntPtr tp = Runtime.PyObject_TYPE(ob);
            int sz = (int)Marshal.ReadIntPtr(tp, TypeOffset.tp_basicsize);

            for (int i = 0; i < sz; i += IntPtr.Size) {
                IntPtr pp = new IntPtr(ob.ToInt64() + i);
                IntPtr v = Marshal.ReadIntPtr(pp);
                Console.WriteLine("offset {0}: {1}", i, v);
            }

            Console.WriteLine("");
            Console.WriteLine("");
        }

        [Conditional("DEBUG")]
        internal static void debug(string msg) {
            StackTrace st = new StackTrace(1, true);
            StackFrame sf = st.GetFrame(0);
            MethodBase mb = sf.GetMethod();
            Type mt = mb.DeclaringType;
            string caller = mt.Name + "." + sf.GetMethod().Name;
            Thread t = Thread.CurrentThread;
            string tid = t.GetHashCode().ToString();
            Console.WriteLine("thread {0} : {1}", tid, caller); 
            Console.WriteLine("  {0}", msg);
            return;
        }


    }


}


