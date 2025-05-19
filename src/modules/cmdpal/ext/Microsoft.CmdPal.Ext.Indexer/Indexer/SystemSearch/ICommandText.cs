// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Indexer.Indexer.OleDB;

namespace Microsoft.CmdPal.Ext.Indexer.Indexer.SystemSearch;

[Guid("0C733A27-2A1C-11CE-ADE5-00AA0044773D")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
public partial interface ICommandText
{
    void Cancel();

    void Execute(IntPtr pUnkOuter, Guid riid, [Optional] IntPtr pParams, [Optional] nint pcRowsAffected, out IRowset ppRowset);

    void GetDBSession(Guid riid, out IntPtr ppSession);

    void GetCommandText([Optional] Guid pguidDialect, out IntPtr ppwszCommand);

    void SetCommandText(Guid rguidDialect, string pwszCommand);
}
