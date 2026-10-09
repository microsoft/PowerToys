// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.CmdPal.Ext.WebSearch.Helpers;

internal static partial class NativeMethods
{
    [LibraryImport("shlwapi.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = false)]
    internal static unsafe partial int AssocQueryStringW(
        AssocF flags,
        AssocStr str,
        string pszAssoc,
        string? pszExtra,
        char* pszOut,
        ref uint pcchOut);

    [Flags]
    public enum AssocF : uint
    {
        None = 0,
        IsProtocol = 0x00001000,
    }

    public enum AssocStr
    {
        Command = 1,
        Executable,
        FriendlyDocName,
        FriendlyAppName,
        NoOpen,
        ShellNewValue,
        DDECommand,
        DDEIfExec,
        DDEApplication,
        DDETopic,
        InfoTip,
        QuickTip,
        TileInfo,
        ContentType,
        DefaultIcon,
        ShellExtension,
        DropTarget,
        DelegateExecute,
        SupportedUriProtocols,
        ProgId,
        AppId,
        AppPublisher,
        AppIconReference, // sometimes present, but DefaultIcon is most common
    }
}
