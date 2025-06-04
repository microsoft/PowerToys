// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using Microsoft.CmdPal.Ext.Indexer.Indexer.SystemSearch;

namespace Microsoft.CmdPal.Ext.Indexer.Native;

public sealed partial class NativeHelpers
{
    public const uint SEEMASKINVOKEIDLIST = 12;

    public const uint CLSCTXINPROCALL = 0x17;

    public struct PropertyKeys
    {
        public static readonly PropertyKey PKEYItemNameDisplay = new() { FmtID = new System.Guid("B725F130-47EF-101A-A5F1-02608C9EEBAC"), PID = 10 };
        public static readonly PropertyKey PKEYItemUrl = new() { FmtID = new System.Guid("49691C90-7E17-101A-A91C-08002B2ECDA9"), PID = 9 };
        public static readonly PropertyKey PKEYKindText = new() { FmtID = new System.Guid("F04BEF95-C585-4197-A2B7-DF46FDC9EE6D"), PID = 100 };
    }

    public static class OleDb
    {
        public static readonly Guid DbGuidDefault = new("C8B521FB-5CF3-11CE-ADE5-00AA0044773D");
    }

    public static class CsWin32GUID
    {
        public static readonly Guid CLSIDSearchManager = new Guid("7D096C5F-AC08-4F1F-BEB7-5C22C517CE39");
        public static readonly Guid IIDISearchManager = new Guid("AB310581-AC80-11D1-8DF3-00C04FB6EF69");
        public static readonly Guid CLSIDCollatorDataSource = new Guid("9E175B8B-F52A-11D8-B9A5-505054503030");
        public static readonly Guid PropertyStore = new Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");
    }
}
