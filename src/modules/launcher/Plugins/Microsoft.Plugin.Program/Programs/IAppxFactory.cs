// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace Microsoft.Plugin.Program.Programs
{
    [Guid("BEB94909-E451-438B-B5A7-D79E767B75D8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IAppxFactory
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:Element should begin with upper-case letter", Justification = "Implements COM Interface")]
        void _VtblGap0_2(); // skip 2 methods

        IAppxManifestReader CreateManifestReader(IStream inputStream);
    }
}
