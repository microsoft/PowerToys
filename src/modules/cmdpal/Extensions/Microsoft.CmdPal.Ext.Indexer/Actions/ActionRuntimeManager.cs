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
    private static readonly Lazy<Task<ActionRuntime>> _lazyRuntime = new(InitializeAsync);

    public static Task<ActionRuntime> InstanceAsync => _lazyRuntime.Value;

    private static async Task<ActionRuntime> InitializeAsync()
    {
        // If we tried 3 times and failed, should we think the action runtime is not working?
        // then we should not use it anymore.
        const int maxAttempts = 3;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var runtime = ActionRuntimeFactory.CreateActionRuntime();
                await Task.Delay(500);

                return runtime;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Attempt {attempt} to initialize ActionRuntime failed: {ex.Message}");

                if (attempt == maxAttempts)
                {
                    Logger.LogError($"Failed to initialize ActionRuntime: {ex.Message}");
                }
            }
        }

        return null;
    }
}
