// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using PowerDisplay.Cli.Commands;
using PowerDisplay.Cli.Errors;
using PowerDisplay.Cli.Options;
using PowerDisplay.Cli.Output;
using PowerDisplay.Common.Services;

namespace PowerDisplay.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Logs go to %LOCALAPPDATA%\Microsoft\PowerToys\PowerDisplay\Logs so any DDC/CI
        // error in the controllers is recoverable post-mortem. CLI text output stays on
        // stdout/stderr and is unaffected by the file logger.
        Logger.InitializeLogger("\\PowerDisplay\\Logs");

        var root = BuildRootCommand();
        var parser = new Parser(root);
        var parseResult = parser.Parse(args);

        // Honour --help / -h / -? on any (sub)command. We let System.CommandLine print
        // and exit; the parse result already has the help-token detected.
        if (parseResult.Tokens.Count == 0 ||
            HasHelpToken(parseResult))
        {
            return await root.InvokeAsync(args);
        }

        var useJson = parseResult.GetValueForOption(CliOptions.Json);
        ICliOutput output = useJson ? new JsonCliOutput() : new TextCliOutput();

        if (parseResult.Errors.Count > 0)
        {
            foreach (var err in parseResult.Errors)
            {
                output.WriteError(new CliErrorResult
                {
                    Command = parseResult.CommandResult.Command.Name,
                    Error = new CliError
                    {
                        Code = CliErrorCodes.ArgumentError,
                        ExitCode = CliExitCodes.ArgumentError,
                        Message = err.Message,
                    },
                });
            }

            return CliExitCodes.ArgumentError;
        }

        try
        {
            using var monitorManager = new MonitorManager();
            using var cts = new CancellationTokenSource();

            var command = parseResult.CommandResult.Command;

            if (command == root.ListCommand)
            {
                return await ListCommand.RunAsync(monitorManager, output, cts.Token);
            }

            if (command == root.CapabilitiesCommand)
            {
                return await CapabilitiesCommand.RunAsync(
                    monitorManager,
                    parseResult.GetValueForOption(CliOptions.MonitorNumber),
                    parseResult.GetValueForOption(CliOptions.MonitorId),
                    output,
                    cts.Token);
            }

            if (command == root.GetCommand)
            {
                return await GetCommand.RunAsync(
                    monitorManager,
                    parseResult.GetValueForOption(CliOptions.MonitorNumber),
                    parseResult.GetValueForOption(CliOptions.MonitorId),
                    parseResult.GetValueForOption(CliOptions.SettingFilter),
                    output,
                    cts.Token);
            }

            if (command == root.SetCommand)
            {
                var inputs = new SetCommandInputs
                {
                    MonitorNumber = parseResult.GetValueForOption(CliOptions.MonitorNumber),
                    MonitorId = parseResult.GetValueForOption(CliOptions.MonitorId),
                    Brightness = parseResult.GetValueForOption(CliOptions.Brightness),
                    Contrast = parseResult.GetValueForOption(CliOptions.Contrast),
                    Volume = parseResult.GetValueForOption(CliOptions.Volume),
                    ColorTemperature = parseResult.GetValueForOption(CliOptions.ColorTemperature),
                    InputSource = parseResult.GetValueForOption(CliOptions.InputSource),
                    PowerState = parseResult.GetValueForOption(CliOptions.PowerState),
                    Orientation = parseResult.GetValueForOption(CliOptions.Orientation),
                };

                return await SetCommand.RunAsync(monitorManager, inputs, output, cts.Token);
            }

            // No subcommand picked → print help.
            return await root.InvokeAsync(args);
        }
        catch (OperationCanceledException)
        {
            output.WriteError(new CliErrorResult
            {
                Command = parseResult.CommandResult.Command.Name,
                Error = new CliError
                {
                    Code = CliErrorCodes.HardwareFailure,
                    ExitCode = CliExitCodes.HardwareFailure,
                    Message = "operation was cancelled",
                },
            });
            return CliExitCodes.HardwareFailure;
        }
        catch (Exception ex)
        {
            Logger.LogError($"PowerDisplay CLI failed: {ex}");
            output.WriteError(new CliErrorResult
            {
                Command = parseResult.CommandResult.Command.Name,
                Error = new CliError
                {
                    Code = CliErrorCodes.HardwareFailure,
                    ExitCode = CliExitCodes.HardwareFailure,
                    Message = $"unexpected error: {ex.Message}",
                },
            });
            return CliExitCodes.HardwareFailure;
        }
    }

    private static PowerDisplayRootCommand BuildRootCommand() => new();

    private static bool HasHelpToken(ParseResult parseResult)
    {
        foreach (var token in parseResult.Tokens)
        {
            switch (token.Value)
            {
                case "--help":
                case "-h":
                case "-?":
                case "/?":
                    return true;
            }
        }

        return false;
    }
}
