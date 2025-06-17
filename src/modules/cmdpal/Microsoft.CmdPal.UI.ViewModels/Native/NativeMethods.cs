// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.CmdPal.UI.ViewModels.Native;

internal sealed partial class NativeMethods
{
    [LibraryImport("ole32.dll")]
    internal static partial int CoInitialize(IntPtr pvReserved);

    [LibraryImport("ole32.dll")]
    internal static partial void CoUninitialize();
}
