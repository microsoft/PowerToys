// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using ManagedCommon;
using PowerToys.Interop;

namespace PowerToysExtension.Helpers;

internal static class KeyboardManagerStateService
{
    private static readonly object Sync = new();
    private static readonly Timer PollingTimer;
    private static readonly RunnerActionClient ActionClient = new();
    private static bool _lastKnownListeningState = IsListening();

    internal static event Action? StatusChanged;

    static KeyboardManagerStateService()
    {
        PollingTimer = new Timer(
            static _ => PollStatus(),
            null,
            TimeSpan.FromMilliseconds(500),
            TimeSpan.FromMilliseconds(500));
    }

    internal static bool IsListening()
    {
        try
        {
            if (Mutex.TryOpenExisting(Constants.KeyboardManagerEngineInstanceMutex(), out var mutex))
            {
                mutex.Dispose();
                return true;
            }
        }
        catch
        {
            // The engine mutex is best-effort state. Treat failures as not listening.
        }

        return false;
    }

    internal static bool TryToggleListening()
    {
        try
        {
            var result = ActionClient.InvokeAction(RunnerActionIds.KeyboardManagerToggleActive);
            PollStatus();
            return result.Success;
        }
        catch
        {
            return false;
        }
    }

    private static void PollStatus()
    {
        var isListening = IsListening();
        var raiseChanged = false;

        lock (Sync)
        {
            if (isListening != _lastKnownListeningState)
            {
                _lastKnownListeningState = isListening;
                raiseChanged = true;
            }
        }

        if (raiseChanged)
        {
            StatusChanged?.Invoke();
        }
    }
}
