// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.CmdPal.Ext.Indexer.Indexer.SystemSearch;

[StructLayout(LayoutKind.Sequential)]
public struct PropertyKey
{
    public Guid FmtID;

    public uint PID;

    public PropertyKey(Guid fmtid, uint pid)
    {
        this.FmtID = fmtid;
        this.PID = pid;
    }
}
