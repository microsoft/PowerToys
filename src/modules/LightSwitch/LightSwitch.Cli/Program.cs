// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LightSwitch.Cli.Ipc;
using ManagedCommon;

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

    public static async Task<int> Main(string[] args)
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
            try
            {
                return CliApplication.WriteUnexpectedFailure(CliCommandLine.HasFlag(args, "--json"), Console.Out, Console.Error);
            }
            catch (Exception)
            {
                // A closed output stream cannot display an error, but should still yield a
                // failure exit code instead of an unhandled exception and raw stack trace.
                return 1;
            }
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
