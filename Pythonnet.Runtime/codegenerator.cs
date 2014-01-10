// ==========================================================================
// This software is subject to the provisions of the Zope Public License,
// Version 2.0 (ZPL).  A copy of the ZPL should accompany this distribution.
// THIS SOFTWARE IS PROVIDED "AS IS" AND ANY AND ALL EXPRESS OR IMPLIED
// WARRANTIES ARE DISCLAIMED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF TITLE, MERCHANTABILITY, AGAINST INFRINGEMENT, AND FITNESS
// FOR A PARTICULAR PURPOSE.
// ==========================================================================

using System;
using System.Threading;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Collections;
using System.Reflection;
using System.Reflection.Emit;

namespace Python.Runtime {

    /// <summary>
    /// Several places in the runtime generate code on the fly to support
    /// dynamic functionality. The CodeGenerator class manages the dynamic
    /// assembly used for code generation and provides utility methods for
    /// certain repetitive tasks.
    /// </summary>

    internal class CodeGenerator {

        AssemblyBuilder aBuilder;
        ModuleBuilder mBuilder;

        internal CodeGenerator() {
            AssemblyName aname = new AssemblyName();
            aname.Name = "__CodeGenerator_Assembly";
            AssemblyBuilderAccess aa = AssemblyBuilderAccess.Run;

            aBuilder = Thread.GetDomain().DefineDynamicAssembly(aname, aa);
            mBuilder = aBuilder.DefineDynamicModule("__CodeGenerator_Module");
        }

        //====================================================================
        // DefineType is a shortcut utility to get a new TypeBuilder.
        //====================================================================

        internal TypeBuilder DefineType(string name) {
            TypeAttributes attrs = TypeAttributes.Public;
            return mBuilder.DefineType(name, attrs);
        }

        //====================================================================
        // DefineType is a shortcut utility to get a new TypeBuilder.
        //====================================================================

        internal TypeBuilder DefineType(string name, Type basetype) {
            TypeAttributes attrs = TypeAttributes.Public;
            return mBuilder.DefineType(name, attrs, basetype);
        }

    }


}
