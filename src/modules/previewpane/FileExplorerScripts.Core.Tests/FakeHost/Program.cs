// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace FileExplorerScripts.Core.Tests.FakeHost;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var rootIndex = Array.IndexOf(args, "--root");
        var consumerIndex = Array.IndexOf(args, "--consumer");
        var root = rootIndex >= 0 ? args[rootIndex + 1] : Environment.GetEnvironmentVariable("POWERSCRIPTS_ROOT");
        if (string.IsNullOrEmpty(root) || consumerIndex < 0 || args[consumerIndex + 1] != "file-explorer")
        {
            Console.Error.Write("Discovery and execution must be attributed to file-explorer.");
            return 2;
        }

        await File.AppendAllTextAsync(Path.Combine(root, "calls.txt"), args[0] + ":" + args[consumerIndex + 1] + Environment.NewLine);
        if (args[0] == "list")
        {
            Console.Write(await File.ReadAllTextAsync(Path.Combine(root, "catalogue.json")));
            return 0;
        }

        if (args[0] == "transform")
        {
            await File.WriteAllTextAsync(Path.Combine(root, "payload.json"), await Console.In.ReadToEndAsync());
            var delay = Path.Combine(root, "delay-ms.txt");
            if (File.Exists(delay))
            {
                await Task.Delay(int.Parse(await File.ReadAllTextAsync(delay), System.Globalization.CultureInfo.InvariantCulture));
            }

            var error = Path.Combine(root, "error.txt");
            if (File.Exists(error))
            {
                Console.Error.Write(await File.ReadAllTextAsync(error));
                return 3;
            }

            Console.Write(await File.ReadAllTextAsync(Path.Combine(root, "result.json")));
            return 0;
        }

        return 2;
    }
}
