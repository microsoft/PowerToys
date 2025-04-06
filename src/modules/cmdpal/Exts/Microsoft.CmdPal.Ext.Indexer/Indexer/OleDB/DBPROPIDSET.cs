// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.CmdPal.Ext.Indexer.Indexer.OleDB;

[StructLayout(LayoutKind.Sequential)]
public struct DBPROPIDSET
{
#pragma warning disable SA1307 // Accessible fields should begin with upper-case letter
    public IntPtr rgPropertyIDs; // Pointer to array of property IDs
    public uint cPropertyIDs;    // Number of properties in array
    public Guid guidPropertySet; // GUID of the property set
#pragma warning restore SA1307 // Accessible fields should begin with upper-case letter
}
