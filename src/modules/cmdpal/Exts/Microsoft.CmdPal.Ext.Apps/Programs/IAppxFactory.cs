// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using Windows.Win32.System.Com;

namespace Microsoft.CmdPal.Ext.Apps.Programs;

[Guid("BEB94909-E451-438B-B5A7-D79E767B75D8")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IAppxFactory
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:Element should begin with upper-case letter", Justification = "Implements COM Interface")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Implements COM Interface")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Implements COM Interface")]
    void _VtblGap0_2(); // skip 2 methods

    internal IAppxManifestReader CreateManifestReader(IStream inputStream);
}
