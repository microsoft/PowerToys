// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Microsoft.CmdPal.Ext.Indexer.Indexer.SystemSearch;

[Guid("0C733A1D-2A1C-11CE-ADE5-00AA0044773D")]
[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
public partial interface IDBCreateCommand
{
    void CreateCommand(IntPtr pUnkOuter, ref Guid riid, out ICommandText ppCommand);
}
