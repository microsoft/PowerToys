// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Ext.WindowWalker.Helpers;

internal sealed record ProcessPackagingInfo(
    int Pid,
    ProcessPackagingKind Kind,
    bool HasPackageIdentity,
    bool IsAppContainer,
    string? PackageFullName,
    int? LastError
)
{
    public static ProcessPackagingInfo Empty { get; } = new(
        Pid: 0,
        Kind: ProcessPackagingKind.Unknown,
        HasPackageIdentity: false,
        IsAppContainer: false,
        PackageFullName: null,
        LastError: null);
}
