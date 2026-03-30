// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.WinGet.Models;

namespace Microsoft.CmdPal.Common.WinGet.Services;

public interface IWinGetOperationTrackerService
{
    /// <summary>
    /// Gets the current and recently completed WinGet operations started by Command Palette.
    /// </summary>
    IReadOnlyList<WinGetPackageOperation> Operations { get; }

    /// <summary>
    /// Raised when a new tracked WinGet operation starts.
    /// </summary>
    event EventHandler<WinGetPackageOperationEventArgs>? OperationStarted;

    /// <summary>
    /// Raised when a tracked WinGet operation reports new progress.
    /// </summary>
    event EventHandler<WinGetPackageOperationEventArgs>? OperationUpdated;

    /// <summary>
    /// Raised when a tracked WinGet operation completes.
    /// </summary>
    event EventHandler<WinGetPackageOperationEventArgs>? OperationCompleted;

    /// <summary>
    /// Gets the newest tracked operation for a WinGet package id.
    /// </summary>
    /// <param name="packageId">The WinGet package id.</param>
    /// <returns>The newest tracked operation for the package, or null when none is tracked.</returns>
    WinGetPackageOperation? GetLatestOperation(string packageId);

    /// <summary>
    /// Requests cancellation for a tracked WinGet operation when supported.
    /// </summary>
    /// <param name="operationId">The tracked operation id.</param>
    /// <returns>True when a cancellation request was issued; otherwise, false.</returns>
    bool TryCancelOperation(Guid operationId);
}
