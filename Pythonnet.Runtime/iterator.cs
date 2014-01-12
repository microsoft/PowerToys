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
    // Implements a generic Python iterator for IEnumerable objects and 
    // managed array objects. This supports 'for i in object:' in Python.
    //========================================================================

    internal class Iterator : ExtensionType {

        IEnumerator iter;

        public Iterator(IEnumerator e) : base() {
            this.iter = e;
        }


        //====================================================================
        // Implements support for the Python iteration protocol.
        //====================================================================

        public static IntPtr tp_iternext(IntPtr ob) {
            Iterator self = GetManagedObject(ob) as Iterator;
            if (!self.iter.MoveNext()) {
                Exceptions.SetError(Exceptions.StopIteration, Runtime.PyNone);
                return IntPtr.Zero;
            }
            object item = self.iter.Current;
            return Converter.ToPythonImplicit(item);
        }

        public static IntPtr tp_iter(IntPtr ob) {
            Runtime.Incref(ob);
            return ob;
        }

    }


}
