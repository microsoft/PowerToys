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

    /// <summary>
    /// Module level properties (attributes)
    /// </summary>
    internal class ModulePropertyObject : ExtensionType {

        public ModulePropertyObject(PropertyInfo md) : base() 
        {
            throw new NotImplementedException("ModulePropertyObject");
        }

    }

}

