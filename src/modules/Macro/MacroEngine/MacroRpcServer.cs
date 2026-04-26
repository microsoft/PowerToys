// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO.Pipes;
using ManagedCommon;
using PowerToys.MacroCommon.Ipc;
using StreamJsonRpc;

namespace PowerToys.MacroEngine;

internal sealed class MacroRpcServer : IMacroEngineRpc
{
    private readonly MacroEngineHost _host;

    public MacroRpcServer(MacroEngineHost host) => _host = host;

    public Task ExecuteMacroAsync(string macroId, CancellationToken ct) =>
        _host.ExecuteMacroByIdAsync(macroId, ct);

    public Task SuspendHotkeysAsync(CancellationToken ct)
    {
        _host.SuspendHotkeys();
        return Task.CompletedTask;
    }

    public Task ResumeHotkeysAsync(CancellationToken ct)
    {
        _host.ResumeHotkeys();
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> GetMacroIdsAsync(CancellationToken ct) =>
        Task.FromResult(_host.GetMacroIds());

    public static async Task RunAsync(MacroEngineHost host, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var pipe = new NamedPipeServerStream(
                MacroIpcConstants.PipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            bool connected = false;
            try
            {
                await pipe.WaitForConnectionAsync(ct);
                connected = true;
            }
            catch (OperationCanceledException)
            {
                pipe.Dispose();
                break;
            }
            catch (Exception ex)
            {
                Logger.LogError("MacroEngine: Named pipe accept failed.", ex);
                pipe.Dispose();
                continue;
            }

            if (connected)
            {
                _ = Task.Run(async () =>
                {
                    using (pipe)
                    {
                        var rpc = JsonRpc.Attach(pipe, new MacroRpcServer(host));
                        await rpc.Completion;
                    }
                }, ct).ContinueWith(
                    t => Logger.LogError("MacroEngine: RPC client session faulted.", t.Exception!.InnerException),
                    TaskContinuationOptions.OnlyOnFaulted);
            }
        }
    }
}
