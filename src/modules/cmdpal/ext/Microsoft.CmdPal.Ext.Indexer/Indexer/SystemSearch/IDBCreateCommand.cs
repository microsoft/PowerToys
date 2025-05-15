// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Microsoft.CmdPal.Ext.Indexer.Indexer.SystemSearch;

[Guid("0C733A8B-2A1C-11CE-ADE5-00AA0044773D")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[GeneratedComInterface]
public partial interface IDBCreateCommand
{
    void CreateCommand(IntPtr pUnkOuter, Guid riid, [MarshalAs(UnmanagedType.Interface)] out ICommandText ppCommand);
}
