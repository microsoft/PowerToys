// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Common.WinGet.Models;

public sealed record WinGetPackageOperation(
    Guid OperationId,
    string PackageId,
    string PackageName,
    WinGetPackageOperationKind Kind,
    WinGetPackageOperationState State,
    bool CanCancel,
    bool IsIndeterminate,
    uint? ProgressPercent,
    ulong? BytesDownloaded,
    ulong? BytesRequired,
    string? ErrorMessage,
    DateTimeOffset StartedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt)
{
    public bool IsCompleted =>
        State is WinGetPackageOperationState.Succeeded
            or WinGetPackageOperationState.Failed
            or WinGetPackageOperationState.Canceled;
}
