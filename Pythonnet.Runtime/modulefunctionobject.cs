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

namespace Python.Runtime
{
    /// <summary>
    /// Module level functions
    /// </summary>
    internal class ModuleFunctionObject : MethodObject
    {

        public ModuleFunctionObject(string name, MethodInfo[] info, bool allow_threads)
            : base(name, info, allow_threads)
        {
            for (int i = 0; i < info.Length; i++)
            {
                MethodInfo item = (MethodInfo)info[i];
                if (!item.IsStatic)
                {
                    throw new Exception("Module function must be static.");
                }
            }
        }

        //====================================================================
        // __call__ implementation.
        //====================================================================

        public static IntPtr tp_call(IntPtr ob, IntPtr args, IntPtr kw)
        {
            ModuleFunctionObject self = (ModuleFunctionObject)GetManagedObject(ob);
            return self.Invoke(ob, args, kw);
        }

        //====================================================================
        // __repr__ implementation.
        //====================================================================

        public static new IntPtr tp_repr(IntPtr ob)
        {
            ModuleFunctionObject self = (ModuleFunctionObject)GetManagedObject(ob);
            string s = String.Format("<CLRModuleFunction '{0}'>", self.name);
            return Runtime.PyString_FromStringAndSize(s, s.Length);
        }

    }
}

