// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using PowerDisplay.Cli.Output;
using PowerDisplay.Cli.Properties;
using PowerDisplay.Contracts;

namespace PowerDisplay.Cli.Ipc;

/// <summary>
/// Encapsulates the common IPC dispatch flow: serialize envelope → send → check
/// provider-unavailable → deserialize response → render → return exit code.
/// <para>
/// The <see cref="SendAsync"/> delegate is injected so the dispatch core can be unit-tested
/// with a stub without standing up a real named-pipe server.
/// </para>
/// </summary>
public sealed class IpcDispatcher
{
    /// <summary>
    /// Signature that matches <see cref="CliPipeClient.SendAsync"/>. Inject a stub in tests.
    /// </summary>
    public delegate Task<string?> SendDelegate(string requestJson, TimeSpan connectTimeout, CancellationToken ct);

    private readonly SendDelegate _send;
    private readonly ICliOutput _output;
    private readonly TimeSpan _connectTimeout;

    public IpcDispatcher(SendDelegate send, ICliOutput output, TimeSpan connectTimeout)
    {
        _send = send;
        _output = output;
        _connectTimeout = connectTimeout;
    }

    /// <summary>
    /// Convenience constructor that uses a real <see cref="CliPipeClient"/> instance.
    /// </summary>
    public IpcDispatcher(ICliOutput output, TimeSpan connectTimeout)
        : this(new CliPipeClient().SendAsync, output, connectTimeout)
    {
    }

    // ── per-command dispatch helpers ─────────────────────────────────────────
    public Task<int> SendListAsync(CliRequestEnvelope envelope, CancellationToken ct)
        => SendAsync(envelope, ContractsJsonContext.Default.CliListResult, _output.WriteListResult, ct);

    public Task<int> SendGetAsync(CliRequestEnvelope envelope, CancellationToken ct)
        => SendAsync(envelope, ContractsJsonContext.Default.CliGetResult, _output.WriteGetResult, ct);

    public Task<int> SendSetAsync(CliRequestEnvelope envelope, CancellationToken ct)
        => SendAsync(envelope, ContractsJsonContext.Default.CliSetResult, _output.WriteSetResult, ct);

    public Task<int> SendCapabilitiesAsync(CliRequestEnvelope envelope, CancellationToken ct)
        => SendAsync(envelope, ContractsJsonContext.Default.CliCapabilitiesResult, _output.WriteCapabilitiesResult, ct);

    public Task<int> SendProfilesAsync(CliRequestEnvelope envelope, CancellationToken ct)
        => SendAsync(envelope, ContractsJsonContext.Default.CliProfileListResult, _output.WriteProfileListResult, ct);

    // up/down reuse the set response shape (CliSetResult before/after) and the set renderer.
    public Task<int> SendAdjustAsync(CliRequestEnvelope envelope, CancellationToken ct)
        => SendAsync(envelope, ContractsJsonContext.Default.CliSetResult, _output.WriteSetResult, ct);

    // apply-profile is best-effort: once the profile exists it always succeeds (exit 0). A missing
    // profile is reported as an error envelope (ARGUMENT_ERROR / exit 7) via the shared error flow.
    public Task<int> SendApplyProfileAsync(CliRequestEnvelope envelope, CancellationToken ct)
        => SendAsync(envelope, ContractsJsonContext.Default.CliApplyProfileResult, _output.WriteApplyProfileResult, ct);

    // All success envelopes map to exit 0 (the shared success path).
    private Task<int> SendAsync<T>(CliRequestEnvelope envelope, JsonTypeInfo<T> typeInfo, Action<T> write, CancellationToken ct)
        where T : class
        => SendAndRenderAsync(envelope, typeInfo, write, static _ => CliExitCodes.Ok, ct);

    // ── core flow ────────────────────────────────────────────────────────────
    private async Task<int> SendAndRenderAsync<T>(
        CliRequestEnvelope envelope,
        JsonTypeInfo<T> typeInfo,
        Action<T> write,
        Func<T, int> exitCode,
        CancellationToken ct)
        where T : class
    {
        var requestJson = JsonSerializer.Serialize(envelope, ContractsJsonContext.Default.CliRequestEnvelope);
        var respJson = await _send(requestJson, _connectTimeout, ct);

        if (respJson is null)
        {
            return WriteProviderUnavailable(envelope.Command);
        }

        // The app stamps an explicit IsError discriminator on every response (see CliResponseHeader):
        // error envelopes set it true; all success DTOs set it false — including apply-profile partial
        // failures, which are still success envelopes and report their outcome via ExitCode. Read the
        // flag first, then deserialize as the matching concrete type.
        var header = TryReadHeader(respJson);

        if (header is { IsError: true })
        {
            try
            {
                var error = JsonSerializer.Deserialize(respJson, ContractsJsonContext.Default.CliErrorResult);
                if (error is not null)
                {
                    _output.WriteError(error);
                    return error.Error.ExitCode;
                }
            }
            catch (JsonException)
            {
            }

            // Flagged as an error but the envelope did not deserialize — treat as a schema mismatch.
            _output.WriteError(BuildInternalError(envelope.Command, Resources.Error_DeserializeMismatch));
            return CliExitCodes.InternalError;
        }

        try
        {
            var result = JsonSerializer.Deserialize(respJson, typeInfo)
                ?? throw new JsonException($"Deserialized {typeof(T).Name} was null.");
            write(result);
            return exitCode(result);
        }
        catch (JsonException)
        {
            // A non-error response that failed to deserialize as the expected success type — likely a
            // schema mismatch between CLI and app versions.
            _output.WriteError(BuildInternalError(envelope.Command, Resources.Error_DeserializeMismatch));
            return CliExitCodes.InternalError;
        }
    }

    private static CliResponseHeader? TryReadHeader(string respJson)
    {
        try
        {
            return JsonSerializer.Deserialize(respJson, ContractsJsonContext.Default.CliResponseHeader);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private int WriteProviderUnavailable(string command)
    {
        _output.WriteError(new CliErrorResult
        {
            Command = command,
            Error = new CliError
            {
                Code = CliErrorCodes.ProviderUnavailable,
                Message = Resources.Error_ProviderUnavailable,
            },
        });
        return CliExitCodes.ProviderUnavailable;
    }

    private static CliErrorResult BuildInternalError(string command, string message) => new()
    {
        Command = command,
        Error = new CliError
        {
            Code = CliErrorCodes.InternalError,
            Message = message,
        },
    };
}
