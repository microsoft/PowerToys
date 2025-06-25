// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Windows.AI.Actions;

namespace Microsoft.CmdPal.Ext.Indexer.Data;

public static class ActionRuntimeManager
{
    private static readonly SemaphoreSlim _initLock = new(1, 1);
    private static readonly TaskCompletionSource<ActionRuntime> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static ActionRuntime _instance;
    private static bool _isInitialized;

    public static bool IsInitialized => _isInitialized;

    public static ActionRuntime Instance =>
        _isInitialized ? _instance : throw new InvalidOperationException("ActionRuntime has not been initialized yet. Call InitializeAsync first.");

    public static Task<ActionRuntime> WaitForRuntimeAsync() => _tcs.Task;

    public static async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        await _initLock.WaitAsync();

        try
        {
            if (_isInitialized)
            {
                return;
            }

            // If we tried 3 times and failed, should we think the action runtime is not working?
            // then we should not use it anymore.
            const int maxAttempts = 3;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    var runtime = ActionRuntimeFactory.CreateActionRuntime();
                    await Task.Delay(500);

                    _instance = runtime;
                    _tcs.SetResult(runtime);
                    return;
                }
                catch (Exception ex)
                {
                    if (attempt == maxAttempts)
                    {
                        Logger.LogError($"Failed to initialize ActionRuntime: {ex.Message}");
                        _tcs.TrySetException(new InvalidOperationException(
                            $"Failed to initialize ActionRuntime after {maxAttempts} attempts.", ex));
                        return;
                    }
                }
            }
        }
        finally
        {
            _isInitialized = true;
            _initLock.Release();
        }
    }
}
