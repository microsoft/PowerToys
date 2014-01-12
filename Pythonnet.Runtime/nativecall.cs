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
using System.Collections;
using System.Reflection;
using System.Reflection.Emit;

namespace Python.Runtime {

    /// <summary>
    /// Provides support for calling native code indirectly through 
    /// function pointers. Most of the important parts of the Python
    /// C API can just be wrapped with p/invoke, but there are some
    /// situations (specifically, calling functions through Python
    /// type structures) where we need to call functions indirectly.
    ///
    /// This class uses Reflection.Emit to generate IJW thunks that
    /// support indirect calls to native code using various common
    /// call signatures. This is mainly a workaround for the fact 
    /// that you can't spell an indirect call in C# (but can in IL).
    ///
    /// Another approach that would work is for this to be turned 
    /// into a separate utility program that could be run during the
    /// build process to generate the thunks as a separate assembly 
    /// that could then be referenced by the main Python runtime.
    /// </summary>

    internal class NativeCall {

        static AssemblyBuilder aBuilder;
        static ModuleBuilder mBuilder;

        public static INativeCall Impl;

        static NativeCall() {

            // The static constructor is responsible for generating the
            // assembly and the methods that implement the IJW thunks.
            //
            // To do this, we actually use reflection on the INativeCall
            // interface (defined below) and generate the required thunk 
            // code based on the method signatures.

            AssemblyName aname = new AssemblyName();
            aname.Name = "e__NativeCall_Assembly";
            AssemblyBuilderAccess aa = AssemblyBuilderAccess.Run;

            aBuilder = Thread.GetDomain().DefineDynamicAssembly(aname, aa);
            mBuilder = aBuilder.DefineDynamicModule("e__NativeCall_Module");

            TypeAttributes ta = TypeAttributes.Public;
            TypeBuilder tBuilder = mBuilder.DefineType("e__NativeCall", ta);

            Type iType = typeof(INativeCall);
            tBuilder.AddInterfaceImplementation(iType);

            // Use reflection to loop over the INativeCall interface methods, 
            // calling GenerateThunk to create a managed thunk for each one.

            foreach (MethodInfo method in iType.GetMethods()) {
                GenerateThunk(tBuilder, method);
            }
            
            Type theType = tBuilder.CreateType();

            Impl = (INativeCall)Activator.CreateInstance(theType);

        }

        private static void GenerateThunk(TypeBuilder tb, MethodInfo method) {

            ParameterInfo[] pi = method.GetParameters();
            int count = pi.Length;
            int argc = count - 1;

            Type[] args = new Type[count];
            for (int i = 0; i < count; i++) {
                args[i] = pi[i].ParameterType;
            }

            MethodBuilder mb = tb.DefineMethod(
                                  method.Name,
                                  MethodAttributes.Public | 
                                  MethodAttributes.Virtual,
                                  method.ReturnType,
                                  args
                                  );

            // Build the method signature for the actual native function.
            // This is essentially the signature of the wrapper method
            // minus the first argument (the passed in function pointer).

            Type[] nargs = new Type[argc];
            for (int i = 1; i < count; i++) {
                nargs[(i - 1)] = args[i];
            }

            // IL generation: the (implicit) first argument of the method 
            // is the 'this' pointer and the second is the function pointer.
            // This code pushes the real args onto the stack, followed by
            // the function pointer, then the calli opcode to make the call.

            ILGenerator il = mb.GetILGenerator();

            for (int i = 0; i < argc; i++) {
                il.Emit(OpCodes.Ldarg_S, (i + 2));
            }

            il.Emit(OpCodes.Ldarg_1);

            il.EmitCalli(OpCodes.Calli, 
                         CallingConvention.Cdecl, 
                         method.ReturnType, 
                         nargs
                         );

            il.Emit(OpCodes.Ret);

            tb.DefineMethodOverride(mb, method);
            return;
        }


        public static void Void_Call_1(IntPtr fp, IntPtr a1) {
            Impl.Void_Call_1(fp, a1);
        }

        public static IntPtr Call_3(IntPtr fp, IntPtr a1, IntPtr a2, 
                                    IntPtr a3) {
            return Impl.Call_3(fp, a1, a2, a3);
        }

        public static int Int_Call_3(IntPtr fp, IntPtr a1, IntPtr a2, 
                                     IntPtr a3) {
            return Impl.Int_Call_3(fp, a1, a2, a3);
        }

    }


    /// <summary>
    /// Defines native call signatures to be generated by NativeCall.
    /// </summary>

    public interface INativeCall {

        void Void_Call_0(IntPtr funcPtr);

        void Void_Call_1(IntPtr funcPtr, IntPtr arg1);

        int Int_Call_3(IntPtr funcPtr, IntPtr t, IntPtr n, IntPtr v);

        IntPtr Call_3(IntPtr funcPtr, IntPtr a1, IntPtr a2, IntPtr a3);

    }

}
