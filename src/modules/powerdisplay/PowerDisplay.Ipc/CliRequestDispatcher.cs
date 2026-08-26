// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PowerDisplay.Common.Services;
using PowerDisplay.Contracts;
using PowerDisplay.Models;
using Monitor = PowerDisplay.Common.Models.Monitor;

namespace PowerDisplay.Ipc;

/// <summary>Routes a parsed CLI request to the matching command handler.</summary>
public static class CliRequestDispatcher
{
    /// <summary>
    /// Builds a serialized response from a request and a snapshot of the host application's state.
    /// </summary>
    public static async Task<string> BuildResponseAsync(
        CliRequestEnvelope envelope,
        IReadOnlyList<Monitor> snapshot,
        IReadOnlySet<string> hiddenIds,
        IReadOnlyList<CustomVcpValueMapping> customMappings,
        IMonitorManager manager,
        int defaultStep,
        Func<CancellationToken, Task<PowerDisplayProfiles>> loadProfilesAsync,
        Func<int, CancellationToken, Task<string?>> applyProfileAsync,
        CancellationToken cancellationToken)
    {
        try
        {
            if (CliCommandHandlers.TryGet(envelope.Command, out var handler))
            {
                var context = new CliCommandContext(
                    envelope,
                    snapshot,
                    hiddenIds,
                    customMappings,
                    manager,
                    defaultStep,
                    loadProfilesAsync,
                    applyProfileAsync);

                return await handler.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
            }

            return CliResponse.SerializeError(
                CliResponse.MakeCodedError(envelope.Command, CliErrorCodes.ArgumentError, CliMessageIds.UnknownCommand, value: envelope.Command));
        }
        catch (OperationCanceledException)
        {
            // Hardware writes cannot be rolled back, so cancellation is reported rather than
            // returning a false success for a potentially partial operation.
            return CliResponse.SerializeError(
                CliResponse.MakeError(envelope.Command, CliErrorCodes.Timeout, "operation timed out or was cancelled"));
        }
    }

    /// <summary>Creates a serialized timeout response for a request that was not dispatched.</summary>
    public static string CreateTimeoutResponse()
        => CliResponse.SerializeError(
            CliResponse.MakeError("unknown", CliErrorCodes.Timeout, "request timed out or was cancelled"));

    /// <summary>Creates a serialized internal-error response for an invalid request envelope.</summary>
    public static string CreateInvalidEnvelopeResponse()
        => CliResponse.SerializeError(
            CliResponse.MakeCodedError("unknown", CliErrorCodes.InternalError, CliMessageIds.InternalError, detail: "could not parse request envelope"));

    /// <summary>Creates a serialized internal-error response for an unexpected host failure.</summary>
    public static string CreateInternalErrorResponse(string detail)
        => CliResponse.SerializeError(
            CliResponse.MakeCodedError("unknown", CliErrorCodes.InternalError, CliMessageIds.InternalError, detail: detail));
}
