// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PowerScripts.Client;

namespace Microsoft.CmdPal.Ext.PowerScripts.UnitTests;

internal sealed class FakePowerScriptsService : IPowerScriptsService
{
    private readonly TaskCompletionSource _runStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _discoverCalls;
    private int _runCalls;

    internal int DiscoverCalls => Volatile.Read(ref _discoverCalls);

    internal int RunCalls => Volatile.Read(ref _runCalls);

    internal PowerScriptsCatalog Catalog { get; set; } = new([], true);

    internal Func<CancellationToken, Task<PowerScriptsCatalog>>? DiscoverHandler { get; set; }

    internal Func<CancellationToken, Task<PowerScriptTransformResult>>? RunHandler { get; set; }

    internal PowerScriptInfo? LastScript { get; private set; }

    internal IReadOnlyDictionary<string, JsonElement>? LastParameters { get; private set; }

    internal CancellationToken RunCancellationToken { get; private set; }

    internal Task RunStarted => _runStarted.Task;

    public Task<PowerScriptsCatalog> DiscoverAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _discoverCalls);
        return DiscoverHandler?.Invoke(cancellationToken) ?? Task.FromResult(Catalog);
    }

    public Task<PowerScriptTransformResult> RunAsync(
        PowerScriptInfo script,
        IReadOnlyDictionary<string, JsonElement> parameters,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _runCalls);
        LastScript = script;
        LastParameters = parameters;
        RunCancellationToken = cancellationToken;
        _runStarted.TrySetResult();
        return RunHandler?.Invoke(cancellationToken) ?? Task.FromResult(new PowerScriptTransformResult());
    }
}
