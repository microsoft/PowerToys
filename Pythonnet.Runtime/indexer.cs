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
using System.Security.Permissions;

namespace Python.Runtime {

    //========================================================================
    // Bundles the information required to support an indexer property.
    //========================================================================

    internal class Indexer {

        public MethodBinder GetterBinder;
        public MethodBinder SetterBinder;

        public Indexer() {
            GetterBinder = new MethodBinder();
            SetterBinder = new MethodBinder();
        }


        public bool CanGet {
            get { 
                return GetterBinder.Count > 0; 
            }
        }

        public bool CanSet {
            get { 
                return SetterBinder.Count > 0; 
            }
        }


        public void AddProperty(PropertyInfo pi) {
            MethodInfo getter = pi.GetGetMethod(true);
            MethodInfo setter = pi.GetSetMethod(true);
            if (getter != null) {
                GetterBinder.AddMethod(getter);
            }
            if (setter != null) {
                SetterBinder.AddMethod(setter);
            }
        }

        internal IntPtr GetItem(IntPtr inst, IntPtr args) {
            return GetterBinder.Invoke(inst, args, IntPtr.Zero);
        }


        internal void SetItem(IntPtr inst, IntPtr args) {
            SetterBinder.Invoke(inst, args, IntPtr.Zero);
        }

    }


}
