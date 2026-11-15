// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using ManagedCommon;
using Microsoft.UI.Dispatching;

namespace PowerOCR.Services;

internal sealed class ActivationService : IActivationService
{
    private readonly DispatcherQueue _dispatcherQueue;

    public ActivationService(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue;
    }

    public event EventHandler? ActivationRequested;

    public void RequestActivation()
    {
        if (_dispatcherQueue.HasThreadAccess)
        {
            ActivationRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (!_dispatcherQueue.TryEnqueue(
            () => ActivationRequested?.Invoke(this, EventArgs.Empty)))
        {
            Logger.LogError("Failed to enqueue the Text Extractor activation request.");
        }
    }
}
