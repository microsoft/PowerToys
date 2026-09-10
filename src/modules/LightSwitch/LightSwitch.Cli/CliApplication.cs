// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LightSwitch.Cli.Protocol;

namespace LightSwitch.Cli;

internal sealed class CliApplication
{
    // Theme changes include several synchronous Windows broadcasts. Keep the overall deadline
    // independent of the shorter connect deadline, and never retry a possibly executed command.
    internal static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(60);

    private readonly Func<string, CancellationToken, Task<string>> _send;
    private readonly Action<string>? _logError;
    private readonly TimeSpan _operationTimeout;

    internal CliApplication(Func<string, CancellationToken, Task<string>> send, Action<string>? logError = null, TimeSpan? operationTimeout = null)
    {
        _send = send ?? throw new ArgumentNullException(nameof(send));
        _logError = logError;
        _operationTimeout = operationTimeout ?? OperationTimeout;
    }

    internal static string CliVersion => typeof(CliApplication).Assembly.GetName().Version?.ToString() ?? "unknown";

    internal async Task<int> RunAsync(string[] args, TextWriter stdout, TextWriter stderr, CancellationToken cancellationToken = default)
    {
        bool json = false;

        try
        {
            var presentation = CliCommandLine.ParsePresentationOptions(args);
            json = presentation.Json;
            if (presentation.Error is not null)
            {
                throw new CliException("INVALID_ARGUMENT", presentation.Error);
            }

            if (presentation.Help)
            {
                WriteInformation(new CliInformation { Help = CliCommandLine.HelpText }, json, stdout);
                return 0;
            }

            if (presentation.Version)
            {
                WriteInformation(new CliInformation { CliVersion = CliVersion }, json, stdout);
                return 0;
            }

            var commandLine = new CliCommandLine();
            var parsed = commandLine.Parse(presentation.Arguments);

            if (parsed.Errors.Count != 0)
            {
                throw new CliException("INVALID_ARGUMENT", string.Join(" ", parsed.Errors.Select(error => error.Message)));
            }

            var request = commandLine.CreateRequest(parsed);
            string requestJson = JsonSerializer.Serialize(request, CliJsonContext.Default.CliRequest);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(_operationTimeout);
            string responseJson = await _send(requestJson, deadline.Token).WaitAsync(deadline.Token).ConfigureAwait(false);
            var response = CliProtocol.ParseResponse(responseJson);
            if (!response.Success)
            {
                LogError($"{response.Error!.Code}: {response.Error.Message}");
            }

            WriteResponse(response, json, stdout, stderr);
            return response.Success ? 0 : CliProtocol.ExitCode(response.Error!.Code);
        }
        catch (OperationCanceledException)
        {
            return WriteFailure(
                "TIMEOUT",
                "The request timed out or was cancelled. It may already have taken effect; run status before trying again.",
                json,
                stdout,
                stderr);
        }
        catch (CliException ex)
        {
            return WriteFailure(ex.Code, ex.Message, json, stdout, stderr);
        }
        catch (Exception ex)
        {
            LogError(ex.ToString());
            return WriteUnexpectedFailure(json, stdout, stderr);
        }
    }

    internal static int WriteUnexpectedFailure(bool json, TextWriter stdout, TextWriter stderr)
    {
        WriteResponse(
            new CliResponse
            {
                Success = false,
                Error = new CliError
                {
                    Code = "EXECUTION_FAILED",
                    Message = "The Light Switch command could not be completed. See the Light Switch log for details.",
                },
            },
            json,
            stdout,
            stderr);
        return 1;
    }

    private static void WriteInformation(CliInformation information, bool json, TextWriter stdout)
    {
        stdout.WriteLine(json
            ? JsonSerializer.Serialize(information, CliJsonContext.Default.CliInformation)
            : information.Help ?? information.CliVersion);
    }

    private static void WriteResponse(CliResponse response, bool json, TextWriter stdout, TextWriter stderr)
    {
        if (json)
        {
            stdout.WriteLine(JsonSerializer.Serialize(response, CliJsonContext.Default.CliResponse));
        }
        else if (!response.Success)
        {
            stderr.WriteLine($"{response.Error!.Code}: {response.Error.Message}");
            if (response.Error.Code == "INVALID_ARGUMENT")
            {
                stderr.WriteLine("Use 'PowerToys.LightSwitch.CLI.exe --help' for usage.");
            }
        }
        else
        {
            var state = response.State!;
            stdout.WriteLine($"System theme: {state.SystemTheme}");
            stdout.WriteLine($"Apps theme: {state.AppsTheme}");
            stdout.WriteLine($"System theme changes: {(state.ChangeSystem ? "enabled" : "disabled")}");
            stdout.WriteLine($"App theme changes: {(state.ChangeApps ? "enabled" : "disabled")}");
            stdout.WriteLine($"Schedule mode: {state.ScheduleMode}");
            stdout.WriteLine($"Manual override: {(state.ManualOverride ? "active" : "inactive")}");
        }
    }

    private int WriteFailure(string code, string message, bool json, TextWriter stdout, TextWriter stderr)
    {
        LogError($"{code}: {message}");
        WriteResponse(new CliResponse { Success = false, Error = new CliError { Code = code, Message = message } }, json, stdout, stderr);
        return CliProtocol.ExitCode(code);
    }

    private void LogError(string message)
    {
        try
        {
            _logError?.Invoke(message);
        }
        catch (Exception)
        {
            // Logging must not corrupt or replace the single CLI response.
        }
    }
}
