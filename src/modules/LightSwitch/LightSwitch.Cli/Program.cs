// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LightSwitch.Cli.Ipc;
using LightSwitch.Cli.Telemetry;
using ManagedCommon;
using Microsoft.PowerToys.Telemetry;

namespace LightSwitch.Cli;

public static class Program
{
    private static readonly Lazy<bool> LoggerInitialized = new(() =>
    {
        try
        {
            Logger.InitializeLogger("\\LightSwitch\\Logs");
        }
        catch (Exception)
        {
            // An unavailable log directory must not replace the CLI error response.
        }

        return true;
    });

    public static Task<int> Main(string[] args)
        => RunWithTelemetryAsync(args, RunAsync, static telemetryEvent => PowerToysTelemetry.Log.WriteEvent(telemetryEvent));

    internal static async Task<int> RunWithTelemetryAsync(
        string[] args,
        Func<string[], Task<int>> run,
        Action<LightSwitchCLICommandEvent> writeTelemetry)
    {
        int exitCode = 1;
        try
        {
            exitCode = await run(args).ConfigureAwait(false);
            return exitCode;
        }
        finally
        {
            try
            {
                writeTelemetry(new LightSwitchCLICommandEvent
                {
                    CommandName = GetTelemetryCommandName(args),
                    Successful = exitCode == 0,
                });
            }
            catch (Exception)
            {
                // Telemetry must not change the command's result or console output.
            }
        }
    }

    internal static string GetTelemetryCommandName(string[] args)
    {
        if (args.Length == 0)
        {
            return "none";
        }

        // Match only known command names; never send user-supplied values or response-file paths.
        return args[0].ToLowerInvariant() switch
        {
            "status" => "status",
            "light" => "light",
            "dark" => "dark",
            "toggle" => "toggle",
            "schedule" => "schedule",
            "--help" or "-h" or "-?" => "help",
            "--version" => "version",
            _ => "unknown",
        };
    }

    private static async Task<int> RunAsync(string[] args)
    {
        try
        {
            try
            {
                Console.OutputEncoding = new UTF8Encoding(false);
            }
            catch (Exception)
            {
                // A host can provide a console whose encoding cannot be changed.
            }

            using var cancellation = new CancellationTokenSource();
            ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
            {
                eventArgs.Cancel = true;
                try
                {
                    cancellation.Cancel();
                }
                catch (ObjectDisposedException)
                {
                }
            };

            Console.CancelKeyPress += cancelHandler;
            try
            {
                // Construct the client only for actual IPC, so local help/version do not even
                // resolve process/pipe details. Both construction and parsing have error guards.
                var application = new CliApplication(
                    static (request, token) => new CliPipeClient().SendAsync(request, token),
                    LogError);
                return await application.RunAsync(args, Console.Out, Console.Error, cancellation.Token).ConfigureAwait(false);
            }
            finally
            {
                Console.CancelKeyPress -= cancelHandler;
            }
        }
        catch (Exception ex)
        {
            LogError(ex.ToString());

            // RunAsync owns error output. If setup or output itself fails, do not reparse
            // response files or attempt another response on an unusable output stream.
            return 1;
        }
    }

    private static void LogError(string message)
    {
        try
        {
            // Parse/validation failures also log, while help/version never initialize a log.
            _ = LoggerInitialized.Value;
            Logger.LogError(message);
        }
        catch (Exception)
        {
        }
    }
}
