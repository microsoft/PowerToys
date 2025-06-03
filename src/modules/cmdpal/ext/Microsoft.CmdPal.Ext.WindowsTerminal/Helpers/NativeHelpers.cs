// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.CmdPal.Ext.WindowsTerminal.Helpers;

public sealed partial class NativeHelpers
{
    public const uint CLSCTXINPROCALL = 0x17;

    [LibraryImport("ole32.dll")]
    public static partial uint CoCreateInstance(
        ref Guid rclsid,
        IntPtr pUnkOuter,
        uint dwClsContext,
        ref Guid riid,
        out IntPtr rReturnedComObject);

#pragma warning disable CA2211 // Non-constant fields should not be visible
#pragma warning disable SA1401 // Fields should be private
    public static Guid ApplicationActivationManagerCLSID = new Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C");
    public static Guid ApplicationActivationManagerIID = new Guid("2e941141-7f97-4756-ba1d-9decde894a3d");
#pragma warning restore SA1401 // Fields should be private
#pragma warning restore CA2211 // Non-constant fields should not be visible
}
