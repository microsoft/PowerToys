// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using Microsoft.CmdPal.Ext.Indexer.Indexer.SystemSearch;

namespace Microsoft.CmdPal.Ext.Indexer.Native;

public sealed partial class NativeHelpers
{
    public const uint SEEMASKINVOKEIDLIST = 12;

    public struct PropertyKeys
    {
        public static readonly PropertyKey PKEYItemNameDisplay = new() { fmtid = new System.Guid("B725F130-47EF-101A-A5F1-02608C9EEBAC"), pid = 10 };
        public static readonly PropertyKey PKEYItemUrl = new() { fmtid = new System.Guid("49691C90-7E17-101A-A91C-08002B2ECDA9"), pid = 9 };
        public static readonly PropertyKey PKEYKindText = new() { fmtid = new System.Guid("F04BEF95-C585-4197-A2B7-DF46FDC9EE6D"), pid = 100 };
    }

    public static class OleDb
    {
        public static readonly Guid DbGuidDefault = new("C8B521FB-5CF3-11CE-ADE5-00AA0044773D");
    }
}
