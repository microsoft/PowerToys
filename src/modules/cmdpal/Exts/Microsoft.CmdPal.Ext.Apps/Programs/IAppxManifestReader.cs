// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.CmdPal.Ext.Apps.Programs;

[Guid("4E1BD148-55A0-4480-A3D1-15544710637C")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IAppxManifestReader
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:Element should begin with upper-case letter", Justification = "Implements COM Interface")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Implements COM Interface")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Implements COM Interface")]
    void _VtblGap0_1(); // skip 1 method

    IAppxManifestProperties GetProperties();

    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:Element should begin with upper-case letter", Justification = "Implements COM Interface")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Implements COM Interface")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Implements COM Interface")]
    void _VtblGap1_5(); // skip 5 methods

    IAppxManifestApplicationsEnumerator GetApplications();
}
