// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerAccent.Core;

internal sealed class DelayedDisplayState
{
    public readonly record struct PendingDisplay(int Generation, int Delay);

    private int _generation;

    public bool IsVisible { get; private set; }

    public PendingDisplay Begin(int displayDelay)
    {
        IsVisible = true;
        return new PendingDisplay(++_generation, displayDelay);
    }

    public bool ShouldShow(PendingDisplay pendingDisplay)
    {
        return IsVisible && pendingDisplay.Generation == _generation;
    }

    public void Cancel()
    {
        IsVisible = false;
        _generation++;
    }
}
