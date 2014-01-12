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
using System.Runtime.InteropServices;

namespace Python.Runtime {

    /// <summary>
    /// xxx
    /// </summary>

    internal interface IReflectedType {
        string PythonTypeName();
        Type GetReflectedType();
    }

    internal interface IReflectedClass : IReflectedType {
        bool IsException();
    }

    internal interface IReflectedInterface : IReflectedType {

    }

    internal interface IReflectedArray : IReflectedType {
    }

    internal interface IReflectedGenericClass : IReflectedClass {
    }


}
