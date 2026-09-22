// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;

namespace PowerToys.TryRun.Cli;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };
        var worker = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Worker", "PowerToys.TryRun.Worker.exe"));
        return await new CliApplication(worker, Console.Out).ExecuteAsync(args, cancellation.Token).ConfigureAwait(false);
    }
}
