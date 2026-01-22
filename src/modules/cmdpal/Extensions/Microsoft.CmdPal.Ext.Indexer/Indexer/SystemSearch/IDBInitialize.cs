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

namespace Microsoft.CmdPal.Ext.Indexer.Indexer.SystemSearch;

[Guid("0C733A8B-2A1C-11CE-ADE5-00AA0044773D")]
[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
public partial interface IDBInitialize
{
    void Initialize();

    void Uninitialize();
}
