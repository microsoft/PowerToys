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
using System.Collections.Specialized;
using System.Runtime.InteropServices;
using System.Reflection;

namespace Python.Runtime {

    //=======================================================================
    // This file defines objects to support binary interop with the Python
    // runtime. Generally, the definitions here need to be kept up to date
    // when moving to new Python versions.
    //=======================================================================

    [Serializable()]
    [AttributeUsage(AttributeTargets.All)]
    public class DocStringAttribute : Attribute {
        public DocStringAttribute(string docStr) {
			DocString = docStr;
		}
		public string DocString {
			get { return docStr; }
			set { docStr = value; }
		}
		private string docStr;
    }
	
    [Serializable()]
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Delegate)]
    internal class PythonMethodAttribute : Attribute {
        public PythonMethodAttribute() {}
    }

    [Serializable()]
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Delegate)]
    internal class ModuleFunctionAttribute : Attribute {
        public ModuleFunctionAttribute() {}
    }

    [Serializable()]
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Delegate)]
    internal class ForbidPythonThreadsAttribute : Attribute {
        public ForbidPythonThreadsAttribute() { }
    }


    [Serializable()]
    [AttributeUsage(AttributeTargets.Property)]
    internal class ModulePropertyAttribute : Attribute {
        public ModulePropertyAttribute() {}
    }


    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Ansi)]
    internal class ObjectOffset {

        static ObjectOffset() {
            int size = IntPtr.Size;
            int n = 0; // Py_TRACE_REFS add two pointers to PyObject_HEAD 
#if (Py_DEBUG)
            _ob_next = 0;
            _ob_prev = 1 * size;
            n = 2;
#endif 
            ob_refcnt = (n+0) * size;
            ob_type = (n+1) * size;
            ob_dict = (n+2) * size;
            ob_data = (n+3) * size;
        }

        public static int magic() {
            return ob_data;
        }

        public static int Size() {
#if (Py_DEBUG)
            return 6 * IntPtr.Size;
#else
            return 4 * IntPtr.Size;
#endif
        }

#if (Py_DEBUG)
        public static int _ob_next;
        public static int _ob_prev;
#endif
        public static int ob_refcnt;
        public static int ob_type;
        public static int ob_dict;
        public static int ob_data;
    }


    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Ansi)]
    internal class TypeOffset {

        static TypeOffset() {
            Type type = typeof(TypeOffset);
            FieldInfo[] fi = type.GetFields();
            int size = IntPtr.Size;
            for (int i = 0; i < fi.Length; i++) {
                fi[i].SetValue(null, i * size);
            }
        }

        public static int magic() {
            return ob_size;
        }
        
/* The *real* layout of a type object when allocated on the heap */
//typedef struct _heaptypeobject {
#if (Py_DEBUG)  // #ifdef Py_TRACE_REFS
/* _PyObject_HEAD_EXTRA defines pointers to support a doubly-linked list of all live heap objects. */
        public static int _ob_next = 0;
        public static int _ob_prev = 0;
#endif
// PyObject_VAR_HEAD {
//     PyObject_HEAD {
        public static int ob_refcnt = 0;
        public static int ob_type = 0;
    // }
        public static int ob_size = 0;      /* Number of items in _VAR_iable part */
// }
        public static int tp_name = 0;      /* For printing, in format "<module>.<name>" */
        public static int tp_basicsize = 0; /* For allocation */
        public static int tp_itemsize = 0;

        /* Methods to implement standard operations */
        public static int tp_dealloc = 0;
        public static int tp_print = 0;
        public static int tp_getattr = 0;
        public static int tp_setattr = 0;
        public static int tp_compare = 0;
        public static int tp_repr = 0;

        /* Method suites for standard classes */
        public static int tp_as_number = 0;
        public static int tp_as_sequence = 0;
        public static int tp_as_mapping = 0;

        /* More standard operations (here for binary compatibility) */
        public static int tp_hash = 0;
        public static int tp_call = 0;
        public static int tp_str = 0;
        public static int tp_getattro = 0;
        public static int tp_setattro = 0;

        /* Functions to access object as input/output buffer */
        public static int tp_as_buffer = 0;

        /* Flags to define presence of optional/expanded features */
        public static int tp_flags = 0;

        public static int tp_doc = 0; /* Documentation string */

        /* Assigned meaning in release 2.0 */
        /* call function for all accessible objects */
        public static int tp_traverse = 0;
        /* delete references to contained objects */
        public static int tp_clear = 0;

        /* Assigned meaning in release 2.1 */
        /* rich comparisons */
        public static int tp_richcompare = 0;
        /* weak reference enabler */
        public static int tp_weaklistoffset = 0;

        /* Added in release 2.2 */
        /* Iterators */
        public static int tp_iter = 0;
        public static int tp_iternext = 0;
        /* Attribute descriptor and subclassing stuff */
        public static int tp_methods = 0;
        public static int tp_members = 0;
        public static int tp_getset = 0;
        public static int tp_base = 0;
        public static int tp_dict = 0;
        public static int tp_descr_get = 0;
        public static int tp_descr_set = 0;
        public static int tp_dictoffset = 0;
        public static int tp_init = 0;
        public static int tp_alloc = 0;
        public static int tp_new = 0;
        public static int tp_free = 0;      /* Low-level free-memory routine */
        public static int tp_is_gc = 0;     /* For PyObject_IS_GC */
        public static int tp_bases = 0;
        public static int tp_mro = 0;       /* method resolution order */
        public static int tp_cache = 0;
        public static int tp_subclasses = 0;
        public static int tp_weaklist = 0;
        public static int tp_del = 0;
#if (PYTHON26 || PYTHON27)
        /* Type attribute cache version tag. Added in version 2.6 */
	    public static int tp_version_tag;
#endif
        // COUNT_ALLOCS adds some more stuff to PyTypeObject 
#if (Py_COUNT_ALLOCS)
	/* these must be last and never explicitly initialized */
        public static int tp_allocs = 0;
        public static int tp_frees = 0;
        public static int tp_maxalloc = 0;
        public static int tp_prev = 0;
        public static int tp_next = 0;
#endif
//} PyTypeObject;
//typedef struct {
        public static int nb_add = 0;
        public static int nb_subtract = 0;
        public static int nb_multiply = 0;
        public static int nb_divide = 0;
        public static int nb_remainder = 0;
        public static int nb_divmod = 0;
        public static int nb_power = 0;
        public static int nb_negative = 0;
        public static int nb_positive = 0;
        public static int nb_absolute = 0;
        public static int nb_nonzero = 0;
        public static int nb_invert = 0;
        public static int nb_lshift = 0;
        public static int nb_rshift = 0;
        public static int nb_and = 0;
        public static int nb_xor = 0;
        public static int nb_or = 0;
        public static int nb_coerce = 0;
        public static int nb_int = 0;
        public static int nb_long = 0;
        public static int nb_float = 0;
        public static int nb_oct = 0;
        public static int nb_hex = 0;
        /* Added in release 2.0 */
        public static int nb_inplace_add = 0;
        public static int nb_inplace_subtract = 0;
        public static int nb_inplace_multiply = 0;
        public static int nb_inplace_divide = 0;
        public static int nb_inplace_remainder = 0;
        public static int nb_inplace_power = 0;
        public static int nb_inplace_lshift = 0;
        public static int nb_inplace_rshift = 0;
        public static int nb_inplace_and = 0;
        public static int nb_inplace_xor = 0;
        public static int nb_inplace_or = 0;
        /* Added in release 2.2 */
        /* The following require the Py_TPFLAGS_HAVE_CLASS flag */
        public static int nb_floor_divide = 0;
        public static int nb_true_divide = 0;
        public static int nb_inplace_floor_divide = 0;
        public static int nb_inplace_true_divide = 0;
#if (PYTHON25 || PYTHON26 || PYTHON27)
        /* Added in release 2.5 */
        public static int nb_index = 0;
#endif
        //} PyNumberMethods;
//typedef struct {
        public static int mp_length = 0;
        public static int mp_subscript = 0;
        public static int mp_ass_subscript = 0;
//} PyMappingMethods;
//typedef struct {
        public static int sq_length = 0;
        public static int sq_concat = 0;
        public static int sq_repeat = 0;
        public static int sq_item = 0;
        public static int sq_slice = 0;
        public static int sq_ass_item = 0;
        public static int sq_ass_slice = 0;
        public static int sq_contains = 0;
        /* Added in release 2.0 */
        public static int sq_inplace_concat = 0;
        public static int sq_inplace_repeat = 0;
//} PySequenceMethods;
//typedef struct {
        public static int bf_getreadbuffer = 0;
        public static int bf_getwritebuffer = 0;
        public static int bf_getsegcount = 0;
        public static int bf_getcharbuffer = 0;
#if (PYTHON26 || PYTHON27)
        // This addition is not actually noted in the 2.6.5 object.h
	    public static int bf_getbuffer = 0;
	    public static int bf_releasebuffer = 0;
//} PyBufferProcs;
#endif
        //PyObject *ht_name, *ht_slots;
        public static int name = 0;
        public static int slots = 0;
        /* here are optional user slots, followed by the members. */
        public static int members = 0;
    }

    /// <summary>
    /// TypeFlags(): The actual bit values for the Type Flags stored
    /// in a class.
    /// Note that the two values reserved for stackless have been put
    /// to good use as PythonNet specific flags (Managed and Subclass)
    /// </summary>
    internal class TypeFlags {
        public static int HaveGetCharBuffer = (1 << 0);
        public static int HaveSequenceIn = (1 << 1);
        public static int GC = 0;
        public static int HaveInPlaceOps = (1 << 3);
        public static int CheckTypes = (1 << 4);
        public static int HaveRichCompare = (1 << 5);
        public static int HaveWeakRefs = (1 << 6);
        public static int HaveIter = (1 << 7);
        public static int HaveClass = (1 << 8);
        public static int HeapType = (1 << 9);
        public static int BaseType = (1 << 10);
        public static int Ready = (1 << 12);
        public static int Readying = (1 << 13);
        public static int HaveGC = (1 << 14);
        // 15 and 16 are reserved for stackless
        public static int HaveStacklessExtension = 0;
        /* XXX Reusing reserved constants */
        public static int Managed = (1 << 15); // PythonNet specific
        public static int Subclass = (1 << 16); // PythonNet specific
#if (PYTHON25 || PYTHON26 || PYTHON27)
        public static int HaveIndex = (1 << 17);
#endif
#if (PYTHON26 || PYTHON27)
        /* Objects support nb_index in PyNumberMethods */
        public static int HaveVersionTag = (1 << 18);
        public static int ValidVersionTag = (1 << 19);
        public static int IsAbstract = (1 << 20);
        public static int HaveNewBuffer = (1 << 21);
        public static int IntSubclass = (1 << 23);
        public static int LongSubclass = (1 << 24);
        public static int ListSubclass = (1 << 25);
        public static int TupleSubclass = (1 << 26);
        public static int StringSubclass = (1 << 27);
        public static int UnicodeSubclass = (1 << 28);
        public static int DictSubclass = (1 << 29);
        public static int BaseExceptionSubclass = (1 << 30);
        public static int TypeSubclass = (1 << 31);
#endif
        public static int Default = (HaveGetCharBuffer |
                             HaveSequenceIn |
                             HaveInPlaceOps |
                             HaveRichCompare |
                             HaveWeakRefs |
                             HaveIter |
                             HaveClass |
                             HaveStacklessExtension |
#if (PYTHON25 || PYTHON26 || PYTHON27)
                             HaveIndex | 
#endif
                             0);
    }


    // This class defines the function prototypes (delegates) used for low
    // level integration with the CPython runtime. It also provides name 
    // based lookup of the correct prototype for a particular Python type 
    // slot and utilities for generating method thunks for managed methods.

    internal class Interop {

        static ArrayList keepAlive;
        static Hashtable pmap;

        static Interop() {

            // Here we build a mapping of PyTypeObject slot names to the
            // appropriate prototype (delegate) type to use for the slot.

            Type[] items = typeof(Interop).GetNestedTypes();
            Hashtable p = new Hashtable();

            for (int i = 0; i < items.Length; i++) {
                Type item = items[i];
                p[item.Name] = item;
            }

            keepAlive = new ArrayList();
            Marshal.AllocHGlobal(IntPtr.Size);
            pmap = new Hashtable();

            pmap["tp_dealloc"] = p["DestructorFunc"];
            pmap["tp_print"] = p["PrintFunc"];
            pmap["tp_getattr"] = p["BinaryFunc"];
            pmap["tp_setattr"] = p["ObjObjArgFunc"];
            pmap["tp_compare"] = p["ObjObjFunc"];
            pmap["tp_repr"] = p["UnaryFunc"];
            pmap["tp_hash"] = p["UnaryFunc"];
            pmap["tp_call"] = p["TernaryFunc"];
            pmap["tp_str"] = p["UnaryFunc"];
            pmap["tp_getattro"] = p["BinaryFunc"];
            pmap["tp_setattro"] = p["ObjObjArgFunc"];
            pmap["tp_traverse"] = p["ObjObjArgFunc"];
            pmap["tp_clear"] = p["InquiryFunc"];
            pmap["tp_richcompare"] = p["RichCmpFunc"];
            pmap["tp_iter"] = p["UnaryFunc"];
            pmap["tp_iternext"] = p["UnaryFunc"];
            pmap["tp_descr_get"] = p["TernaryFunc"];
            pmap["tp_descr_set"] = p["ObjObjArgFunc"];
            pmap["tp_init"] = p["ObjObjArgFunc"];
            pmap["tp_alloc"] = p["IntArgFunc"];
            pmap["tp_new"] = p["TernaryFunc"];
            pmap["tp_free"] = p["DestructorFunc"];
            pmap["tp_is_gc"] = p["InquiryFunc"];

            pmap["nb_add"] = p["BinaryFunc"];
            pmap["nb_subtract"] = p["BinaryFunc"];
            pmap["nb_multiply"] = p["BinaryFunc"];
            pmap["nb_divide"] = p["BinaryFunc"];
            pmap["nb_remainder"] = p["BinaryFunc"];
            pmap["nb_divmod"] = p["BinaryFunc"];
            pmap["nb_power"] = p["TernaryFunc"];
            pmap["nb_negative"] = p["UnaryFunc"];
            pmap["nb_positive"] = p["UnaryFunc"];
            pmap["nb_absolute"] = p["UnaryFunc"];
            pmap["nb_nonzero"] = p["InquiryFunc"];
            pmap["nb_invert"] = p["UnaryFunc"];
            pmap["nb_lshift"] = p["BinaryFunc"];
            pmap["nb_rshift"] = p["BinaryFunc"];
            pmap["nb_and"] = p["BinaryFunc"];
            pmap["nb_xor"] = p["BinaryFunc"];
            pmap["nb_or"] = p["BinaryFunc"];
            pmap["nb_coerce"] = p["ObjObjFunc"];
            pmap["nb_int"] = p["UnaryFunc"];
            pmap["nb_long"] = p["UnaryFunc"];
            pmap["nb_float"] = p["UnaryFunc"];
            pmap["nb_oct"] = p["UnaryFunc"];
            pmap["nb_hex"] = p["UnaryFunc"];
            pmap["nb_inplace_add"] = p["BinaryFunc"];
            pmap["nb_inplace_subtract"] = p["BinaryFunc"];
            pmap["nb_inplace_multiply"] = p["BinaryFunc"];
            pmap["nb_inplace_divide"] = p["BinaryFunc"];
            pmap["nb_inplace_remainder"] = p["BinaryFunc"];
            pmap["nb_inplace_power"] = p["TernaryFunc"];
            pmap["nb_inplace_lshift"] = p["BinaryFunc"];
            pmap["nb_inplace_rshift"] = p["BinaryFunc"];
            pmap["nb_inplace_and"] = p["BinaryFunc"];
            pmap["nb_inplace_xor"] = p["BinaryFunc"];
            pmap["nb_inplace_or"] = p["BinaryFunc"];
            pmap["nb_floor_divide"] = p["BinaryFunc"];
            pmap["nb_true_divide"] = p["BinaryFunc"];
            pmap["nb_inplace_floor_divide"] = p["BinaryFunc"];
            pmap["nb_inplace_true_divide"] = p["BinaryFunc"];
#if (PYTHON25 || PYTHON26 || PYTHON27)
            pmap["nb_index"] = p["UnaryFunc"];
#endif

            pmap["sq_length"] = p["InquiryFunc"];
            pmap["sq_concat"] = p["BinaryFunc"];
            pmap["sq_repeat"] = p["IntArgFunc"];
            pmap["sq_item"] = p["IntArgFunc"];
            pmap["sq_slice"] = p["IntIntArgFunc"];
            pmap["sq_ass_item"] = p["IntObjArgFunc"];
            pmap["sq_ass_slice"] = p["IntIntObjArgFunc"];
            pmap["sq_contains"] = p["ObjObjFunc"];
            pmap["sq_inplace_concat"] = p["BinaryFunc"];
            pmap["sq_inplace_repeat"] = p["IntArgFunc"];

            pmap["mp_length"] = p["InquiryFunc"];
            pmap["mp_subscript"] = p["BinaryFunc"];
            pmap["mp_ass_subscript"] = p["ObjObjArgFunc"];
            
            pmap["bf_getreadbuffer"] = p["IntObjArgFunc"];
            pmap["bf_getwritebuffer"] = p["IntObjArgFunc"];
            pmap["bf_getsegcount"] = p["ObjObjFunc"];
            pmap["bf_getcharbuffer"] = p["IntObjArgFunc"];

            pmap["__import__"] = p["TernaryFunc"];
        }

        internal static Type GetPrototype(string name) {
            return pmap[name] as Type;
        }

        internal static IntPtr GetThunk(MethodInfo method) {
            Type dt = Interop.GetPrototype(method.Name);
            if (dt != null) {
                IntPtr tmp = Marshal.AllocHGlobal(IntPtr.Size);
                Delegate d = Delegate.CreateDelegate(dt, method);
                Thunk cb = new Thunk(d);
                Marshal.StructureToPtr(cb, tmp, false);
                IntPtr fp = Marshal.ReadIntPtr(tmp, 0);
                Marshal.FreeHGlobal(tmp);
                keepAlive.Add(d);
                return fp;
            }
            return IntPtr.Zero;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate IntPtr UnaryFunc(IntPtr ob);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate IntPtr BinaryFunc(IntPtr ob, IntPtr arg);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate IntPtr TernaryFunc(IntPtr ob, IntPtr a1, IntPtr a2);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int InquiryFunc(IntPtr ob);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate IntPtr IntArgFunc(IntPtr ob, int arg);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate IntPtr IntIntArgFunc(IntPtr ob, int a1, int a2);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int IntObjArgFunc(IntPtr ob, int a1, IntPtr a2);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int IntIntObjArgFunc(IntPtr o, int a, int b, IntPtr c);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int ObjObjArgFunc(IntPtr o, IntPtr a, IntPtr b);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int ObjObjFunc(IntPtr ob, IntPtr arg);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void DestructorFunc(IntPtr ob);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int PrintFunc(IntPtr ob, IntPtr a, int b);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate IntPtr RichCmpFunc(IntPtr ob, IntPtr a, int b);

    }


    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Ansi)]
    internal struct Thunk {
        public Delegate fn;

        public Thunk(Delegate d) {
            fn = d;
        }
    }

}
