// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using PowerDisplay.Contracts;

namespace PowerDisplay.Ipc;

/// <summary>
/// Shared serialization + error-envelope helpers for the CLI IPC command handlers. Centralizing
/// these collapses the per-command "check the error, otherwise serialize the result" boilerplate the
/// dispatcher used to repeat for every command, so a new result DTO needs no new plumbing here.
/// </summary>
internal static class CliResponse
{
    /// <summary>
    /// Serializes a response DTO to one-line JSON using its source-generated
    /// <see cref="JsonTypeInfo{T}"/> (AOT/trim safe).
    /// </summary>
    public static string Serialize<T>(T value, JsonTypeInfo<T> typeInfo)
        => JsonSerializer.Serialize(value, typeInfo);

    /// <summary>Serializes a <see cref="CliErrorResult"/> envelope to one-line JSON.</summary>
    public static string SerializeError(CliErrorResult error)
        => JsonSerializer.Serialize(error, ContractsJsonContext.Default.CliErrorResult);

    /// <summary>
    /// Serializes the populated half of a projector/executor <c>(Result, Error)</c> tuple: the error
    /// envelope when present, otherwise the success DTO. Exactly one is expected to be non-null.
    /// </summary>
    public static string ResultOrError<T>((T? Result, CliErrorResult? Error) outcome, JsonTypeInfo<T> typeInfo)
        where T : class
        => outcome.Error is not null ? SerializeError(outcome.Error) : Serialize(outcome.Result!, typeInfo);

    /// <summary>
    /// Free-text error: the app supplies the human-readable <paramref name="message"/> directly (used
    /// for the internal/argument faults the CLI does not localize via a message id).
    /// </summary>
    public static CliErrorResult MakeError(string command, string code, string message)
        => new()
        {
            Command = command,
            Error = new CliError
            {
                Code = code,
                Message = message,
            },
        };

    /// <summary>
    /// Code-only error: the app names the message via <see cref="CliMessageIds"/> and supplies
    /// structured data; the CLI localizes the human-readable text. Value/Detail feed the template.
    /// </summary>
    public static CliErrorResult MakeCodedError(string command, string code, string messageId, string? value = null, string? detail = null)
        => new()
        {
            Command = command,
            Error = new CliError
            {
                Code = code,
                MessageId = messageId,
                Value = value,
                Detail = detail,
            },
        };
}
