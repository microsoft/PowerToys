// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;
using PowerToys.MacroEngine;

Logger.InitializeLogger("\\Macro\\Logs");

if (args.Length > 0 && int.TryParse(args[0], out int parentPid))
{
    RunnerHelper.WaitForPowerToysRunner(parentPid, () => Environment.Exit(0));
}

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

using var host = new MacroEngineHost();
await host.StartAsync(cts.Token);

await MacroRpcServer.RunAsync(host, cts.Token);
