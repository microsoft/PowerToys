// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LightSwitch.Cli.Properties;
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
        CliInformation? information = null;
        CliResponse? response = null;

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
                information = new CliInformation { Help = CliCommandLine.HelpText };
            }
            else if (presentation.Version)
            {
                information = new CliInformation { CliVersion = CliVersion };
            }
            else
            {
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
                response = CliProtocol.ParseResponse(responseJson);
                if (!response.Success)
                {
                    LogError($"{response.Error!.Code}: {response.Error.Message}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            response = CreateFailure("TIMEOUT", Resources.Error_TimedOut);
        }
        catch (CliException ex)
        {
            response = CreateFailure(ex.Code, ex.Message);
        }
        catch (Exception ex)
        {
            LogError(ex.ToString());
            response = new CliResponse
            {
                Success = false,
                Error = new CliError { Code = "EXECUTION_FAILED", Message = Resources.Error_UnexpectedFailure },
            };
        }

        // Output failures escape to Main's log/exit guard. Retrying here could append
        // a second envelope after an output stream accepted only part of the first.
        if (information is not null)
        {
            WriteInformation(information, json, stdout);
            return 0;
        }

        WriteResponse(response!, json, stdout, stderr);
        return response!.Success ? 0 : CliProtocol.ExitCode(response.Error!.Code);
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
                stderr.WriteLine(Resources.Hint_Usage);
            }
        }
        else
        {
            var state = response.State!;
            stdout.WriteLine(Resources.Text_SystemTheme(ThemeDisplayName(state.SystemTheme)));
            stdout.WriteLine(Resources.Text_AppsTheme(ThemeDisplayName(state.AppsTheme)));
            stdout.WriteLine(Resources.Text_ChangeSystem(state.ChangeSystem ? Resources.Value_Enabled : Resources.Value_Disabled));
            stdout.WriteLine(Resources.Text_ChangeApps(state.ChangeApps ? Resources.Value_Enabled : Resources.Value_Disabled));
            stdout.WriteLine(Resources.Text_ScheduleMode(ScheduleModeDisplayName(state.ScheduleMode)));
            stdout.WriteLine(Resources.Text_ManualOverride(state.ManualOverride ? Resources.Value_Active : Resources.Value_Inactive));
        }
    }

    // Only text output uses display names; the JSON state retains the protocol values.
    private static string ThemeDisplayName(string theme) => theme switch
    {
        "light" => Resources.Value_Light,
        "dark" => Resources.Value_Dark,
        _ => Resources.Value_Unknown,
    };

    private static string ScheduleModeDisplayName(string mode) => mode switch
    {
        "Off" => Resources.Value_Off,
        "FixedHours" => Resources.Value_FixedHours,
        "SunsetToSunrise" => Resources.Value_SunsetToSunrise,
        "FollowNightLight" => Resources.Value_FollowNightLight,
        _ => mode,
    };

    private CliResponse CreateFailure(string code, string message)
    {
        LogError($"{code}: {message}");
        return new CliResponse { Success = false, Error = new CliError { Code = code, Message = message } };
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
