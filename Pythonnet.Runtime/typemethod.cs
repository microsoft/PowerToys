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
    // Implements a Python type that provides access to CLR object methods.
    //========================================================================

    internal class TypeMethod : MethodObject {

        public TypeMethod(string name, MethodInfo[] info) : 
               base(name, info) {}

        public TypeMethod(string name, MethodInfo[] info, bool allow_threads) :
               base(name, info, allow_threads) { }

        public override IntPtr Invoke(IntPtr ob, IntPtr args, IntPtr kw) {
            MethodInfo mi = this.info[0];
            Object[] arglist = new Object[3];
            arglist[0] = ob;
            arglist[1] = args;
            arglist[2] = kw;

            try {        
                Object inst = null;
                if (ob != IntPtr.Zero) {
                    inst = GetManagedObject(ob);
                }
                return (IntPtr)mi.Invoke(inst, BindingFlags.Default, null, arglist, 
                                 null);
            }
            catch (Exception e) {
                Exceptions.SetError(e);
                return IntPtr.Zero;
            }
        }



    }


}
