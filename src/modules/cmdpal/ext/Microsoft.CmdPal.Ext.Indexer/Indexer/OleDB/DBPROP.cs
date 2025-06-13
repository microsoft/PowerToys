// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using Microsoft.CmdPal.Ext.Indexer.Indexer.SystemSearch;
using Windows.Win32.Storage.IndexServer;

namespace Microsoft.CmdPal.Ext.Indexer.Indexer.OleDB;

[StructLayout(LayoutKind.Sequential)]
internal struct DBPROP
{
#pragma warning disable SA1307 // Accessible fields should begin with upper-case letter
    public uint dwPropertyID;
    public uint dwOptions;
    public uint dwStatus;
    public DBID colid;
    public PropVariant vValue;
#pragma warning restore SA1307 // Accessible fields should begin with upper-case letter
}
