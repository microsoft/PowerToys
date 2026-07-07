// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace PowerDisplay.Ipc;

/// <summary>
/// Handles one CLI IPC command (list, get, set, …). Each handler owns the payload check, the
/// projector/executor call, and the response serialization for its command, so the dispatcher only
/// routes by command name instead of carrying a per-command switch arm.
/// </summary>
internal interface ICliCommandHandler
{
    /// <summary>
    /// Executes the command against the pre-fetched <paramref name="context"/> and returns the
    /// one-line JSON response. Expected error conditions are returned as an error envelope rather than
    /// thrown; an <see cref="OperationCanceledException"/> from a cancelled hardware write is allowed
    /// to propagate and is mapped to <c>TIMEOUT</c> by the dispatcher.
    /// </summary>
    /// <param name="context">The per-request snapshot of ViewModel/MonitorManager state.</param>
    /// <param name="ct">Cancellation token (Ctrl+C / server timeout).</param>
    Task<string> ExecuteAsync(CliCommandContext context, CancellationToken ct);
}
