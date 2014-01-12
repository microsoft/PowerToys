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
using System.Security.Permissions;
using System.Runtime.InteropServices;
using System.Resources;

[assembly: System.Reflection.AssemblyProduct("Python for .NET")]
[assembly: System.Reflection.AssemblyVersion("2.0.0.2")]
[assembly: AssemblyDefaultAliasAttribute("Python.Runtime.dll")]
[assembly: CLSCompliant(true)]
[assembly: ComVisible(false)]


[assembly:PermissionSetAttribute(SecurityAction.RequestMinimum, 
                                 Name = "FullTrust")]
[assembly: AssemblyCopyrightAttribute("Zope Public License, Version 2.0 (ZPL)")]
[assembly: AssemblyFileVersionAttribute("2.0.0.2")]
[assembly: NeutralResourcesLanguageAttribute("en")]

#if (PYTHON23)
[assembly: AssemblyTitleAttribute("Python.Runtime for Python 2.3")]
[assembly: AssemblyDescriptionAttribute("Python Runtime for Python 2.3")]
#endif
#if (PYTHON24)
[assembly: AssemblyTitleAttribute("Python.Runtime for Python 2.4")]
[assembly: AssemblyDescriptionAttribute("Python Runtime for Python 2.4")]
#endif
#if (PYTHON25)
[assembly: AssemblyTitleAttribute("Python.Runtime for Python 2.5")]
[assembly: AssemblyDescriptionAttribute("Python Runtime for Python 2.5")]
#endif
#if (PYTHON26)
[assembly: AssemblyTitleAttribute("Python.Runtime for Python 2.6")]
[assembly: AssemblyDescriptionAttribute("Python Runtime for Python 2.6")]
#endif
#if (PYTHON27)
[assembly: AssemblyTitle("Python.Runtime for Python 2.7")]
[assembly: AssemblyDescription("Python Runtime for Python 2.7")]
#endif
