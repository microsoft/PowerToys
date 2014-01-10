// ==========================================================================
// This software is subject to the provisions of the Zope Public License,
// Version 2.0 (ZPL).  A copy of the ZPL should accompany this distribution.
// THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
// WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
// FOR A PARTICULAR PURPOSE.
// ==========================================================================

using System;
using System.Reflection;

namespace Python.Runtime {

    //========================================================================
    // A ConstructorBinder encapsulates information about one or more managed
    // constructors, and is responsible for selecting the right constructor
    // given a set of Python arguments. This is slightly different than the
    // standard MethodBinder because of a difference in invoking constructors
    // using reflection (which is seems to be a CLR bug).
    //========================================================================

    internal class ConstructorBinder : MethodBinder {

        internal ConstructorBinder () : base() {}

        //====================================================================
        // Constructors get invoked when an instance of a wrapped managed
        // class or a subclass of a managed class is created. This differs
        // from the MethodBinder implementation in that we return the raw
        // result of the constructor rather than wrapping it as a Python 
        // object - the reason is that only the caller knows the correct
        // Python type to use when wrapping the result (may be a subclass).
        //====================================================================

        internal object InvokeRaw(IntPtr inst, IntPtr args, IntPtr kw) {
            return this.InvokeRaw(inst, args, kw, null);
        }
        /// <summary>
        /// Allows ctor selection to be limited to a single attempt at a
        /// match by providing the MethodBase to use instead of searching
        /// the entire MethodBinder.list (generic ArrayList)
        /// </summary>
        /// <param name="inst"> (possibly null) instance </param>
        /// <param name="args"> PyObject* to the arg tuple </param>
        /// <param name="kw"> PyObject* to the keyword args dict </param>
        /// <param name="info"> The sole ContructorInfo to use or null </param>
        /// <returns> The result of the constructor call with converted params </returns>
        /// <remarks>
        /// 2010-07-24 BC: I added the info parameter to the call to Bind()
        /// Binding binding = this.Bind(inst, args, kw, info);
        /// to take advantage of Bind()'s ability to use a single MethodBase (CI or MI).
        /// </remarks>
        internal object InvokeRaw(IntPtr inst, IntPtr args, IntPtr kw,
                                  MethodBase info) {
            Binding binding = this.Bind(inst, args, kw, info);
            Object result;

            if (binding == null) {
                // It is possible for __new__ to be invoked on construction
                // of a Python subclass of a managed class, so args may
                // reflect more args than are required to instantiate the
                // class. So if we cant find a ctor that matches, we'll see
                // if there is a default constructor and, if so, assume that
                // any extra args are intended for the subclass' __init__.

                IntPtr eargs = Runtime.PyTuple_New(0);
                binding = this.Bind(inst, eargs, kw);
                Runtime.Decref(eargs);

                if (binding == null) {
                    Exceptions.SetError(Exceptions.TypeError,
                                        "no constructor matches given arguments"
                                        );
                    return null;
                }
            }

            // Fire the selected ctor and catch errors...
            ConstructorInfo ci = (ConstructorInfo)binding.info;
            // Object construction is presumed to be non-blocking and fast
            // enough that we shouldn't really need to release the GIL.
            try {
                result = ci.Invoke(binding.args);
            }
            catch (Exception e) {
                if (e.InnerException != null) {
                    e = e.InnerException;
                }
                Exceptions.SetError(e);
                return null;
            }
            return result;
        }
    }
}
