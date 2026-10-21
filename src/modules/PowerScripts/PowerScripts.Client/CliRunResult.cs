// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

namespace PowerScripts.Client;

/// <summary>The structured outcome of a built-in CLI invocation through the PowerScripts host.</summary>
public sealed class CliRunResult
{
    /// <summary>Whether the requested CLI operation succeeded.</summary>
    public bool Succeeded { get; init; }

    /// <summary>The host's outcome token; consumers must tolerate future status values.</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>The CLI or host failure exit code.</summary>
    public int ExitCode { get; init; }

    /// <summary>The invoked CLI's standard output.</summary>
    public string StdOut { get; init; } = string.Empty;

    /// <summary>The invoked CLI's standard error.</summary>
    public string StdErr { get; init; } = string.Empty;

    /// <summary>A diagnostic explaining a rejected or failed invocation.</summary>
    public string? Error { get; init; }

    /// <summary>
    /// Parses the host's JSON envelope without reflection. The envelope's exit code is preserved
    /// even when nonzero, and malformed responses return an explicit protocol failure.
    /// </summary>
    public static CliRunResult Parse(string json, int hostExitCode = PowerScriptsProtocol.ExitCode.Ok) => Parse(json, hostExitCode, string.Empty);

    internal static CliRunResult Parse(string json, int hostExitCode, string hostStdErr)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return InvalidResponse(hostExitCode, hostStdErr);
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("succeeded", out var succeeded) ||
                succeeded.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
                !root.TryGetProperty("status", out var status) ||
                status.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(status.GetString()) ||
                !root.TryGetProperty("exitCode", out var exitCode) ||
                exitCode.ValueKind != JsonValueKind.Number ||
                !exitCode.TryGetInt32(out var numericExitCode) ||
                !root.TryGetProperty("stdOut", out var stdOut) ||
                stdOut.ValueKind != JsonValueKind.String ||
                !root.TryGetProperty("stdErr", out var stdErr) ||
                stdErr.ValueKind != JsonValueKind.String)
            {
                return InvalidResponse(hostExitCode, hostStdErr);
            }

            if (succeeded.ValueKind == JsonValueKind.True &&
                (numericExitCode != PowerScriptsProtocol.ExitCode.Ok || hostExitCode != PowerScriptsProtocol.ExitCode.Ok))
            {
                return InvalidResponse(numericExitCode != PowerScriptsProtocol.ExitCode.Ok ? numericExitCode : hostExitCode, hostStdErr);
            }

            return new CliRunResult
            {
                Succeeded = succeeded.ValueKind == JsonValueKind.True,
                Status = status.GetString()!,
                ExitCode = numericExitCode,
                StdOut = stdOut.GetString() ?? string.Empty,
                StdErr = stdErr.GetString() ?? string.Empty,
                Error = root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String
                    ? error.GetString()
                    : null,
            };
        }
        catch (JsonException)
        {
            return InvalidResponse(hostExitCode, hostStdErr);
        }
    }

    private static CliRunResult InvalidResponse(int hostExitCode, string hostStdErr) => new()
    {
        Status = "invalidResponse",
        ExitCode = hostExitCode == PowerScriptsProtocol.ExitCode.Ok ? PowerScriptsProtocol.ExitCode.Error : hostExitCode,
        StdErr = hostStdErr,
        Error = "The PowerScripts host returned an invalid CLI run response.",
    };
}
