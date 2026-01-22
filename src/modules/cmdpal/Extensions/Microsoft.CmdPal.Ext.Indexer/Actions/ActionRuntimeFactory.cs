// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ManagedCsWin32;
using WinRT;

namespace Microsoft.CmdPal.Ext.Indexer.Data;

internal static class ActionRuntimeFactory
{
    private const string ActionRuntimeClsidStr = "C36FEF7E-35F3-4192-9F2C-AF1FD425FB85";

    // typeof(Windows.AI.Actions.IActionRuntime).GUID
    private static readonly Guid IActionRuntimeIID = Guid.Parse("206EFA2C-C909-508A-B4B0-9482BE96DB9C");

    public static unsafe global::Windows.AI.Actions.ActionRuntime CreateActionRuntime()
    {
        IntPtr abiPtr = default;
        try
        {
            Guid classId = Guid.Parse(ActionRuntimeClsidStr);
            Guid iid = IActionRuntimeIID;

            var hresult = Ole32.CoCreateInstance(ref Unsafe.AsRef(in classId), IntPtr.Zero, CLSCTX.LocalServer, ref iid, out abiPtr);
            Marshal.ThrowExceptionForHR((int)hresult);

            return MarshalInterface<global::Windows.AI.Actions.ActionRuntime>.FromAbi(abiPtr);
        }
        finally
        {
            MarshalInspectable<object>.DisposeAbi(abiPtr);
        }
    }
}
